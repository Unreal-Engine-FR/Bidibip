using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bidibip.Plugin.Sdk;
using Bidibip.Plugin.Sdk.Permissions;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace Bidibip.Plugins.Reglement.Commands;

public sealed class ReglementModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DiscordSocketClient _client;
    private readonly BotConfig _botConfig;

    public ReglementModule(DiscordSocketClient client, BotConfig botConfig)
    {
        _client = client;
        _botConfig = botConfig;
    }

    [SlashCommand("reglement", "Poster ou rafraichir le reglement")]
    [AllowedBotRole(BotRole.Moderator)]
    public async Task ReglementAsync(
        [Summary("fichier", "Fichier JSON contenant le reglement")] IAttachment attachment)
    {
        var config = await LoadConfigAsync();

        if (config.ReglementChannel == 0)
        {
            await FollowupAsync("Le canal du reglement n'est pas configure.", ephemeral: true);
            return;
        }

        var guild = Context.Guild;
        var reglementChannel = guild.GetTextChannel(config.ReglementChannel);
        if (reglementChannel is null)
        {
            await FollowupAsync("Le canal du reglement est introuvable.", ephemeral: true);
            return;
        }

        // Download the JSON attachment
        using var httpClient = new HttpClient();
        var jsonContent = await httpClient.GetStringAsync(attachment.Url);

        ReglementData? data;
        try
        {
            data = JsonSerializer.Deserialize<ReglementData>(jsonContent);
        }
        catch (JsonException ex)
        {
            await FollowupAsync($"Erreur lors de la lecture du fichier JSON : {ex.Message}", ephemeral: true);
            return;
        }

        if (data?.Messages is null || data.Messages.Count == 0)
        {
            await FollowupAsync("Le fichier JSON ne contient aucun message.", ephemeral: true);
            return;
        }

        // Delete old messages in the rules channel (last 100)
        var oldMessages = await reglementChannel.GetMessagesAsync(100).FlattenAsync();
        foreach (var msg in oldMessages)
        {
            await msg.DeleteAsync();
        }

        // Post new messages
        foreach (var messageData in data.Messages)
        {
            var text = messageData.Textes is not null
                ? string.Join("\n", messageData.Textes)
                : null;

            var embeds = new List<Embed>();
            if (messageData.Embeds is not null)
            {
                foreach (var embedData in messageData.Embeds)
                {
                    var embed = new EmbedBuilder()
                        .WithTitle(embedData.Titre)
                        .WithDescription(embedData.Description)
                        .Build();
                    embeds.Add(embed);
                }
            }

            if (!string.IsNullOrEmpty(text) || embeds.Count > 0)
            {
                await reglementChannel.SendMessageAsync(
                    text: text,
                    embeds: embeds.Count > 0 ? embeds.ToArray() : null
                );
            }
        }

        // Add approval button at the end
        var components = new ComponentBuilder()
            .WithButton("Accepter le reglement", "reglement::approve", ButtonStyle.Success)
            .Build();

        await reglementChannel.SendMessageAsync(
            "Clique sur le bouton ci-dessous pour accepter le reglement :",
            components: components
        );

        await FollowupAsync("Le reglement a ete poste avec succes !", ephemeral: true);
    }

    [ComponentInteraction("reglement::approve")]
    [AllowedBotRole(BotRole.Everyone)]
    public async Task ApproveReglementAsync()
    {
        await DeferAsync(ephemeral: true);
        var memberRoleId = _botConfig.Roles.Member;
        var guild = Context.Guild;
        var user = Context.User as SocketGuildUser;

        if (user is null)
        {
            await FollowupAsync("Impossible de verifier ton role.", ephemeral: true);
            return;
        }

        if (user.Roles.Any(r => r.Id == memberRoleId))
        {
            await FollowupAsync("Tu as deja accepte le reglement.", ephemeral: true);
            return;
        }

        var memberRole = guild.GetRole(memberRoleId);
        if (memberRole is null)
        {
            await FollowupAsync("Le role membre est introuvable.", ephemeral: true);
            return;
        }

        await user.AddRoleAsync(memberRole);
        await FollowupAsync("Bienvenue !", ephemeral: true);
    }

    private async Task<ReglementConfig> LoadConfigAsync()
    {
        var configPath = Path.Combine(ReglementPlugin.DataPath, "config.json");
        if (File.Exists(configPath))
        {
            var json = await File.ReadAllTextAsync(configPath);
            return JsonSerializer.Deserialize<ReglementConfig>(json, PluginJsonOptions.Default) ?? new ReglementConfig();
        }

        var defaultConfig = new ReglementConfig();
        Directory.CreateDirectory(ReglementPlugin.DataPath);
        var defaultJson = JsonSerializer.Serialize(defaultConfig, PluginJsonOptions.Default);
        await File.WriteAllTextAsync(configPath, defaultJson);
        return defaultConfig;
    }
}

internal sealed class ReglementConfig
{
    [JsonPropertyName("reglement_channel")]
    public ulong ReglementChannel { get; set; }
}

internal sealed class ReglementData
{
    [JsonPropertyName("messages")]
    public List<ReglementMessage>? Messages { get; set; }
}

internal sealed class ReglementMessage
{
    [JsonPropertyName("textes")]
    public List<string>? Textes { get; set; }

    [JsonPropertyName("embeds")]
    public List<ReglementEmbed>? Embeds { get; set; }
}

internal sealed class ReglementEmbed
{
    [JsonPropertyName("titre")]
    public string? Titre { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}
