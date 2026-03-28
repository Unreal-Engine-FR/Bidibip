using Bidibip.Plugin.Sdk;
using Bidibip.Plugin.Sdk.Permissions;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace Bidibip.Plugins.AntiSpam.Commands;

public sealed class AntiSpamModule : InteractionModuleBase<SocketInteractionContext>
{
    [ComponentInteraction("antispam_kick_*")]
    [AllowedBotRole(BotRole.Moderator)]
    public async Task KickUserAsync()
    {
        await DeferAsync();

        var component = (IComponentInteraction)Context.Interaction;
        var config = await LoadConfigAsync();
        var messageId = component.Message.Id.ToString();

        if (!config.Spammers.TryGetValue(messageId, out var spammer) ||
            spammer.KickButton != component.Data.CustomId)
        {
            await FollowupAsync("Interaction invalide.", ephemeral: true);
            return;
        }

        var guild = Context.Guild;
        var user = guild.GetUser(spammer.Spammer);

        if (user is not null)
        {
            await user.KickAsync("Spam détecté");
        }

        // Delete the moderation message
        await component.Message.DeleteAsync();

        // Respond with result
        await FollowupAsync(
            $"<@{spammer.Spammer}> a été kick par {Context.User.Mention} pour cause de spam");

        // Clean up config
        config.Spammers.Remove(messageId);
        await SaveConfigAsync(config);
    }

    [ComponentInteraction("antispam_pardon_*")]
    [AllowedBotRole(BotRole.Moderator)]
    public async Task PardonUserAsync()
    {
        await DeferAsync();

        var component = (IComponentInteraction)Context.Interaction;
        var config = await LoadConfigAsync();
        var messageId = component.Message.Id.ToString();

        if (!config.Spammers.TryGetValue(messageId, out var spammer) ||
            spammer.PardonButton != component.Data.CustomId)
        {
            await FollowupAsync("Interaction invalide.", ephemeral: true);
            return;
        }

        var guild = Context.Guild;
        var user = guild.GetUser(spammer.Spammer);

        // Remove mute role (use the antispam config's mute_role, same as what was applied)
        if (user is not null && config.MuteRole != 0)
        {
            await user.RemoveRoleAsync(config.MuteRole);
        }

        // Respond with result
        await FollowupAsync(
            $"<@{spammer.Spammer}> a été pardonné par {Context.User.Mention}");

        // Delete the moderation message
        await component.Message.DeleteAsync();

        // Clear user spam history so they aren't immediately re-flagged
        AntiSpamPlugin.ClearUserHistory(spammer.Spammer);

        // Clean up config
        config.Spammers.Remove(messageId);
        await SaveConfigAsync(config);
    }

    private static string ConfigPath => Path.Combine(AntiSpamPlugin.DataPath, "config.json");

    private async Task<AntiSpamConfig> LoadConfigAsync() =>
        await PluginData.LoadAsync<AntiSpamConfig>(ConfigPath);

    private async Task SaveConfigAsync(AntiSpamConfig config) =>
        await PluginData.SaveAsync(ConfigPath, config);
}
