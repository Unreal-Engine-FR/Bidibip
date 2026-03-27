using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace Bidibip.Plugins.AntiSpam.Commands;

public sealed class AntiSpamModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DiscordSocketClient _client;

    public AntiSpamModule(DiscordSocketClient client)
    {
        _client = client;
    }

    [ComponentInteraction("antispam::kick::*")]
    public async Task KickUserAsync()
    {
        await DeferAsync(ephemeral: true);
        var customId = ((IComponentInteraction)Context.Interaction).Data.CustomId;
        var parts = customId.Split("::");
        if (parts.Length < 3 || !ulong.TryParse(parts[2], out var userId))
        {
            await FollowupAsync("ID utilisateur invalide.", ephemeral: true);
            return;
        }

        var guild = Context.Guild;
        var user = guild.GetUser(userId);

        if (user is null)
        {
            await FollowupAsync($"Utilisateur `{userId}` introuvable sur le serveur.", ephemeral: true);
            return;
        }

        try
        {
            // Remove timeout before kicking
            await user.RemoveTimeOutAsync();
        }
        catch
        {
            // Timeout may already have expired
        }

        try
        {
            await user.KickAsync($"Spam detecte - kick par {Context.User.Username}");
            await FollowupAsync($"L'utilisateur {user.Username} (`{userId}`) a ete kick.");
        }
        catch (Exception ex)
        {
            await FollowupAsync($"Erreur lors du kick : {ex.Message}", ephemeral: true);
        }
    }

    [ComponentInteraction("antispam::pardon::*")]
    public async Task PardonUserAsync()
    {
        await DeferAsync(ephemeral: true);
        var customId = ((IComponentInteraction)Context.Interaction).Data.CustomId;
        var parts = customId.Split("::");
        if (parts.Length < 3 || !ulong.TryParse(parts[2], out var userId))
        {
            await FollowupAsync("ID utilisateur invalide.", ephemeral: true);
            return;
        }

        var guild = Context.Guild;
        var user = guild.GetUser(userId);

        if (user is null)
        {
            await FollowupAsync($"Utilisateur `{userId}` introuvable sur le serveur.", ephemeral: true);
            return;
        }

        try
        {
            await user.RemoveTimeOutAsync();
            await FollowupAsync($"L'utilisateur {user.Username} (`{userId}`) a ete pardonne, timeout retire.");
        }
        catch (Exception ex)
        {
            await FollowupAsync($"Erreur lors du pardon : {ex.Message}", ephemeral: true);
        }
    }
}
