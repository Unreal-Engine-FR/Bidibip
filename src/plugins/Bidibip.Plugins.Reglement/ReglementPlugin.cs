using System.Text.Json;
using System.Text.Json.Serialization;
using Bidibip.Plugin.Sdk;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace Bidibip.Plugins.Reglement;

[BidibipPlugin]
public sealed class ReglementPlugin : IBidibipPlugin
{
    public string Name => "Reglement";
    public string Description => "Outil de mise à jour automatique du réglement";

    private ILogger _logger = null!;
    private BotConfig _botConfig = null!;
    private ReglementConfig _config = new();

    public async Task InitializeAsync(PluginContext context)
    {
        _logger = context.Logger;
        _botConfig = context.BotConfig;

        var configPath = Path.Combine(context.DataPath, "config.json");
        _config = await PluginData.LoadAsync<ReglementConfig>(configPath);

        if (_config.ReglementChannel == 0)
        {
            _logger.LogWarning("Reglement channel is not configured");
            return;
        }

        context.Events.OnMessageReceived(HandleMessageAsync);
        context.Events.OnInteractionCreated(HandleInteractionAsync);
    }

    private async Task HandleMessageAsync(IMessage message)
    {
        if (message.Channel.Id != _config.ReglementChannel)
            return;

        var attachment = message.Attachments.FirstOrDefault();
        if (attachment is null)
            return;

        using var httpClient = new HttpClient();
        string jsonContent;
        try
        {
            jsonContent = await httpClient.GetStringAsync(attachment.Url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download reglement json");
            return;
        }

        JsonMessageData? data;
        try
        {
            data = JsonSerializer.Deserialize<JsonMessageData>(jsonContent);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to convert json to message");
            return;
        }

        if (data?.Messages is null || data.Messages.Count == 0)
            return;

        // Delete old messages
        var channel = message.Channel;
        try
        {
            var oldMessages = await channel.GetMessagesAsync(100).FlattenAsync();
            foreach (var msg in oldMessages)
                await msg.DeleteAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete old messages");
        }

        // Send new messages
        foreach (var msg in data.Messages)
        {
            if ((msg.Textes is null || msg.Textes.Count == 0) &&
                (msg.Embeds is null || msg.Embeds.Count == 0))
            {
                _logger.LogWarning("Chaque message doit contenir au moins un message ou au moins un embed");
                continue;
            }

            string? text = null;
            if (msg.Textes is { Count: > 0 })
            {
                text = string.Join("\n", msg.Textes) + "\n";
                if (text.Length > 2000)
                    text = text[..2000];
            }

            Embed[]? embeds = null;
            if (msg.Embeds is { Count: > 0 })
            {
                embeds = msg.Embeds.Select(e =>
                {
                    var builder = new EmbedBuilder().WithTitle(e.Titre);
                    if (e.Description is not null)
                    {
                        var desc = e.Description.Length > 4096
                            ? e.Description[..4096]
                            : e.Description;
                        builder.WithDescription(desc);
                    }
                    return builder.Build();
                }).ToArray();
            }

            MessageComponent? components = null;
            if (msg.Interactions is { Count: > 0 })
            {
                var builder = new ComponentBuilder();
                foreach (var interaction in msg.Interactions)
                {
                    if (interaction.Bouton is not null)
                    {
                        var style = interaction.Bouton.Type?.ToLowerInvariant() switch
                        {
                            "primary" => ButtonStyle.Primary,
                            "secondary" => ButtonStyle.Secondary,
                            "success" => ButtonStyle.Success,
                            "danger" => ButtonStyle.Danger,
                            _ => ButtonStyle.Primary
                        };

                        builder.WithButton(
                            label: interaction.Bouton.Texte,
                            customId: interaction.Bouton.Identifiant,
                            style: style);
                    }
                }
                components = builder.Build();
            }

            try
            {
                await channel.SendMessageAsync(
                    text: text,
                    embeds: embeds,
                    components: components);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send new reglement message");
            }
        }
    }

    private async Task HandleInteractionAsync(SocketInteraction interaction)
    {
        if (interaction is not SocketMessageComponent component)
            return;

        if (component.Data.CustomId != "reglement_approval")
            return;

        if (interaction.User is not SocketGuildUser guildUser)
            return;

        try
        {
            await guildUser.AddRoleAsync(_botConfig.Roles.Member);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to give member role");
        }

        try
        {
            await component.DeferAsync(ephemeral: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to defer command interaction");
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class ReglementConfig
{
    [JsonPropertyName("reglement_channel")]
    public ulong ReglementChannel { get; set; }
}

internal sealed class JsonMessageData
{
    [JsonPropertyName("messages")]
    public List<JsonMessageEntry>? Messages { get; set; }
}

internal sealed class JsonMessageEntry
{
    [JsonPropertyName("textes")]
    public List<string>? Textes { get; set; }

    [JsonPropertyName("embeds")]
    public List<JsonMessageEmbed>? Embeds { get; set; }

    [JsonPropertyName("interactions")]
    public List<JsonMessageInteraction>? Interactions { get; set; }
}

internal sealed class JsonMessageEmbed
{
    [JsonPropertyName("titre")]
    public string? Titre { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

internal sealed class JsonMessageInteraction
{
    [JsonPropertyName("bouton")]
    public JsonMessageButton? Bouton { get; set; }
}

internal sealed class JsonMessageButton
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("texte")]
    public string? Texte { get; set; }

    [JsonPropertyName("identifiant")]
    public string? Identifiant { get; set; }
}
