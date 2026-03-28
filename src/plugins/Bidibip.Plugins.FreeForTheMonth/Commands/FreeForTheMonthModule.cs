using Bidibip.Plugin.Sdk;
using Bidibip.Plugin.Sdk.Permissions;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace Bidibip.Plugins.FreeForTheMonth.Commands;

public sealed class FreeForTheMonthModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("freeforthemonth", "Voir les assets gratuits du mois sur Fab")]
    [AllowedBotRole(BotRole.Member)]
    public async Task FreeForTheMonthAsync(
        [Summary("subscribe", "S'abonner ou se desabonner des notifications")]
        [Choice("Oui", "yes")]
        [Choice("Non", "no")]
        string? subscribe = null)
    {
        var config = await PluginData.LoadAsync<FreeForTheMonthConfig>(FreeForTheMonthPlugin.ConfigPath);

        // Handle subscription
        if (subscribe is not null && config.NotifyFfmRole != 0)
        {
            var guildUser = (IGuildUser)Context.User;
            var hasRole = guildUser.RoleIds.Contains(config.NotifyFfmRole);

            if (subscribe == "yes" && !hasRole)
            {
                await guildUser.AddRoleAsync(config.NotifyFfmRole);
                await FollowupAsync("Tu es maintenant abonne aux notifications Free For The Month !", ephemeral: true);
            }
            else if (subscribe == "yes" && hasRole)
            {
                await FollowupAsync("Tu es deja abonne !", ephemeral: true);
            }
            else if (subscribe == "no" && hasRole)
            {
                await guildUser.RemoveRoleAsync(config.NotifyFfmRole);
                await FollowupAsync("Tu es desabonne des notifications Free For The Month.", ephemeral: true);
            }
            else
            {
                await FollowupAsync("Tu n'es pas abonne.", ephemeral: true);
            }
        }

        // Show current free listings
        List<FabListing> listings;
        try
        {
            listings = await FabListingService.FetchListingsAsync();
        }
        catch (Exception ex)
        {
            FreeForTheMonthPlugin.Logger.LogError(ex, "Failed to fetch Fab listings");
            await FollowupAsync("Impossible de recuperer les donnees de Fab.", ephemeral: true);
            return;
        }

        if (listings.Count == 0)
        {
            await FollowupAsync("Aucun asset gratuit en ce moment.", ephemeral: true);
            return;
        }

        var endDate = listings.FirstOrDefault()?.DiscountEnd;
        var endStr = endDate.HasValue
            ? $"<t:{endDate.Value.ToUnixTimeSeconds()}:R>"
            : "bientot";

        var embeds = FreeForTheMonthPlugin.BuildEmbeds(listings);

        await FollowupAsync(
            $"**Assets gratuits sur Fab** (expire {endStr})\nhttps://www.fab.com/limited-time-free",
            embeds: embeds,
            ephemeral: true);
    }
}
