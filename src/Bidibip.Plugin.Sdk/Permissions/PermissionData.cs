using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace Bidibip.Plugin.Sdk.Permissions;

/// <summary>
/// Stores the fetched Discord permission bitfields for each configured role.
/// Used to compute <c>DefaultMemberPermissions</c> for command registration,
/// reproducing the Rust implementation's approach:
///   - Fetch actual role permissions from the guild at startup
///   - Compute intersections across tiers so commands are visible to the correct roles
/// </summary>
public sealed class PermissionData
{
    private GuildPermissions _memberPermissions;
    private GuildPermissions _helperPermissions;
    private GuildPermissions _moderatorPermissions;
    private GuildPermissions _administratorPermissions;

    public bool IsLoaded { get; private set; }

    /// <summary>
    /// Returns the Discord permission set required for "at least this role" visibility.
    /// The intersection ensures that all roles at the target tier and above share
    /// these permission bits, so Discord shows the command to all qualifying users.
    /// </summary>
    public GuildPermission? AtLeast(BotRole role) => role switch
    {
        BotRole.Administrator => (GuildPermission)_administratorPermissions.RawValue,
        BotRole.Moderator => (GuildPermission)(_moderatorPermissions.RawValue & _administratorPermissions.RawValue),
        BotRole.Helper => (GuildPermission)(_helperPermissions.RawValue & _moderatorPermissions.RawValue & _administratorPermissions.RawValue),
        // Member and Everyone: no Discord-side restriction (visible to all)
        _ => null,
    };

    /// <summary>
    /// Fetches the actual Discord permissions for each configured role from the guild.
    /// Must be called after the bot is connected (on Ready).
    /// Mirrors the Rust implementation's fetch_roles() behavior.
    /// </summary>
    public Task FetchRolesAsync(DiscordSocketClient client, BotConfig config, ILogger logger)
    {
        if (config.GuildId == 0)
        {
            logger.LogWarning("GuildId is not configured, cannot fetch role permissions");
            return Task.CompletedTask;
        }

        var guild = client.GetGuild(config.GuildId);
        if (guild is null)
        {
            logger.LogError("Guild {GuildId} not found, cannot fetch role permissions", config.GuildId);
            return Task.CompletedTask;
        }

        var memberRole = guild.GetRole(config.Roles.Member);
        var helperRole = guild.GetRole(config.Roles.Helper);
        var moderatorRole = guild.GetRole(config.Roles.Moderator);
        var adminRole = guild.GetRole(config.Roles.Administrator);

        if (memberRole is null) logger.LogError("Member role {RoleId} not found in guild", config.Roles.Member);
        if (helperRole is null) logger.LogError("Helper role {RoleId} not found in guild", config.Roles.Helper);
        if (moderatorRole is null) logger.LogError("Moderator role {RoleId} not found in guild", config.Roles.Moderator);
        if (adminRole is null) logger.LogError("Administrator role {RoleId} not found in guild", config.Roles.Administrator);

        // Fetch raw permission bits, expanding Administrator flag to all permissions
        // so intersections work correctly (Administrator implies all permissions)
        _memberPermissions = ExpandAdmin(memberRole?.Permissions ?? GuildPermissions.None);
        _helperPermissions = ExpandAdmin(helperRole?.Permissions ?? GuildPermissions.None);
        _moderatorPermissions = ExpandAdmin(moderatorRole?.Permissions ?? GuildPermissions.None);
        _administratorPermissions = ExpandAdmin(adminRole?.Permissions ?? GuildPermissions.None);

        // Validate: warn if roles have identical permissions (same issue the Rust code detected)
        if (_memberPermissions.RawValue == _administratorPermissions.RawValue)
            logger.LogError("Member and Administrator roles have identical permissions!");
        if (_helperPermissions.RawValue == _administratorPermissions.RawValue)
            logger.LogError("Helper and Administrator roles have identical permissions!");
        if (_memberPermissions.RawValue == _helperPermissions.RawValue)
            logger.LogError("Member and Helper roles have identical permissions!");
        if (_moderatorPermissions.RawValue == _administratorPermissions.RawValue)
            logger.LogError("Moderator and Administrator roles have identical permissions!");

        IsLoaded = true;

        logger.LogInformation(
            "Role permissions loaded — Member: {Member}, Helper: {Helper}, Moderator: {Moderator}, Admin: {Admin}",
            _memberPermissions.RawValue,
            _helperPermissions.RawValue,
            _moderatorPermissions.RawValue,
            _administratorPermissions.RawValue);

        return Task.CompletedTask;
    }

    /// <summary>
    /// If the permission set includes the Administrator flag, expand to all permissions.
    /// This is necessary because Administrator implies all permissions in Discord,
    /// but the raw bits may only contain the Administrator flag (0x8).
    /// Without this expansion, intersections with admin would yield near-zero results.
    /// </summary>
    private static GuildPermissions ExpandAdmin(GuildPermissions perms)
    {
        return perms.Administrator ? GuildPermissions.All : perms;
    }
}
