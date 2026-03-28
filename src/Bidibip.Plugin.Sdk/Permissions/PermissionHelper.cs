using Discord.WebSocket;

namespace Bidibip.Plugin.Sdk.Permissions;

/// <summary>
/// Centralized permission checks. Every command execution path must go through this helper.
/// </summary>
public static class PermissionHelper
{
    /// <summary>
    /// Checks whether a guild user meets the minimum role requirement.
    /// A user meets the requirement if they hold ANY role whose tier is >= <paramref name="minimumRole"/>.
    /// Server owner always passes.
    /// </summary>
    public static bool HasRole(SocketGuildUser user, BotRole minimumRole, BotConfig config)
    {
        // Everyone passes the Everyone check
        if (minimumRole == BotRole.Everyone)
            return true;

        // Server owner always has full access
        if (user.Guild.OwnerId == user.Id)
            return true;

        // Determine the highest role tier this user holds
        var userTier = GetHighestRole(user, config);

        return userTier >= minimumRole;
    }

    /// <summary>
    /// Returns the highest <see cref="BotRole"/> the user currently holds
    /// based on their Discord role IDs compared to the configured role IDs.
    /// </summary>
    public static BotRole GetHighestRole(SocketGuildUser user, BotConfig config)
    {
        var highest = BotRole.Everyone;

        foreach (var roleId in user.Roles.Select(r => r.Id))
        {
            if (roleId == config.Roles.Administrator && BotRole.Administrator > highest)
                highest = BotRole.Administrator;
            else if (roleId == config.Roles.Moderator && BotRole.Moderator > highest)
                highest = BotRole.Moderator;
            else if (roleId == config.Roles.Helper && BotRole.Helper > highest)
                highest = BotRole.Helper;
            else if (roleId == config.Roles.Member && BotRole.Member > highest)
                highest = BotRole.Member;
        }

        return highest;
    }
}
