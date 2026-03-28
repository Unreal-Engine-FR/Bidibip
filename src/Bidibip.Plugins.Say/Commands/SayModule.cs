using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bidibip.Plugin.Sdk.Permissions;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace Bidibip.Plugins.Say.Commands;

public class SayModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("say", "Envoie un message via le bot dans le canal actuel")]
    [AllowedBotRole(BotRole.Member)]
    public async Task SayAsync(
        [Summary("message", "Le texte à envoyer")] string message)
    {
        await Context.Channel.SendMessageAsync(message);
        await FollowupAsync("Message envoyé.", ephemeral: true);
    }

    [SlashCommand("say-file", "Envoie des messages formatés depuis un fichier JSON")]
    [AllowedBotRole(BotRole.Helper)]
    public async Task SayFileAsync(
        [Summary("fichier", "Le fichier JSON contenant les messages")] IAttachment fichier)
    {
        if (!fichier.Filename.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            await FollowupAsync("Le fichier doit être au format JSON.", ephemeral: true);
            return;
        }

        using var httpClient = new HttpClient();
        SayFileData? data;

        try
        {
            var json = await httpClient.GetStringAsync(fichier.Url);
            data = JsonSerializer.Deserialize<SayFileData>(json);
        }
        catch (Exception ex)
        {
            await FollowupAsync($"Erreur lors de la lecture du fichier : {ex.Message}", ephemeral: true);
            return;
        }

        if (data?.Messages is null || data.Messages.Length == 0)
        {
            await FollowupAsync("Le fichier ne contient aucun message.", ephemeral: true);
            return;
        }

        foreach (var msg in data.Messages)
        {
            var text = msg.Textes is { Length: > 0 }
                ? string.Join('\n', msg.Textes)
                : null;

            var embeds = msg.Embeds is { Length: > 0 }
                ? msg.Embeds.Select(BuildEmbed).ToArray()
                : null;

            var components = msg.Interactions is { Length: > 0 }
                ? BuildComponents(msg.Interactions)
                : null;

            await Context.Channel.SendMessageAsync(
                text: text,
                embeds: embeds,
                components: components);
        }

        await FollowupAsync("Messages envoyés.", ephemeral: true);
    }

    private static Embed BuildEmbed(SayEmbed source)
    {
        var builder = new EmbedBuilder();

        if (!string.IsNullOrWhiteSpace(source.Titre))
            builder.WithTitle(source.Titre);

        if (!string.IsNullOrWhiteSpace(source.Description))
            builder.WithDescription(source.Description);

        if (!string.IsNullOrWhiteSpace(source.Couleur))
        {
            if (uint.TryParse(source.Couleur.TrimStart('#'), System.Globalization.NumberStyles.HexNumber, null, out var colorValue))
                builder.WithColor(new Color(colorValue));
        }

        return builder.Build();
    }

    private static MessageComponent BuildComponents(SayInteraction[] interactions)
    {
        var builder = new ComponentBuilder();

        foreach (var interaction in interactions)
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
                    label: interaction.Bouton.Texte ?? "Bouton",
                    customId: interaction.Bouton.Identifiant ?? Guid.NewGuid().ToString(),
                    style: style);
            }
        }

        return builder.Build();
    }
}

public sealed class SayFileData
{
    [JsonPropertyName("messages")]
    public SayMessage[]? Messages { get; set; }
}

public sealed class SayMessage
{
    [JsonPropertyName("textes")]
    public string[]? Textes { get; set; }

    [JsonPropertyName("embeds")]
    public SayEmbed[]? Embeds { get; set; }

    [JsonPropertyName("interactions")]
    public SayInteraction[]? Interactions { get; set; }
}

public sealed class SayEmbed
{
    [JsonPropertyName("titre")]
    public string? Titre { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("couleur")]
    public string? Couleur { get; set; }
}

public sealed class SayInteraction
{
    [JsonPropertyName("bouton")]
    public SayButton? Bouton { get; set; }
}

public sealed class SayButton
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("texte")]
    public string? Texte { get; set; }

    [JsonPropertyName("identifiant")]
    public string? Identifiant { get; set; }
}
