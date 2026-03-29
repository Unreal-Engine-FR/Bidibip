namespace Bidibip.Plugin.Sdk;

/// <summary>
/// Typed representation of the <c>Bot</c> section in <c>Saved/config.json</c>.
/// Bound from JSON at startup via <c>IConfiguration.GetSection("Bot").Bind()</c>.
/// All IDs default to 0 (meaning "not configured").
/// </summary>
public sealed class BotConfig
{
    /// <summary>
    /// The Discord server (guild) ID. When set, slash commands are registered as
    /// guild commands (instant updates). When 0, commands are registered globally
    /// (can take up to 1 hour to propagate — recommended for production).
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>Discord role IDs mapped to the bot's permission tiers.</summary>
    public RolesConfig Roles { get; set; } = new();

    /// <summary>Discord channel IDs used by the host and plugins.</summary>
    public ChannelsConfig Channels { get; set; } = new();
}

/// <summary>
/// Maps the bot's abstract permission tiers to actual Discord role IDs.
/// See <see cref="Permissions.BotRole"/> for the tier hierarchy.
/// </summary>
public sealed class RolesConfig
{
    /// <summary>Full access to all commands (tier 4).</summary>
    public ulong Administrator { get; set; }

    /// <summary>Moderation commands — warn, mute, kick, ban (tier 3).</summary>
    public ulong Moderator { get; set; }

    /// <summary>Support staff commands (tier 2).</summary>
    public ulong Helper { get; set; }

    /// <summary>Verified member — typically assigned after accepting rules (tier 1).</summary>
    public ulong Member { get; set; }

    /// <summary>Assigned to muted users by the AntiSpam plugin. Not a permission tier.</summary>
    public ulong Mute { get; set; }

    /// <summary>Pinged in the log channel when an error occurs. Not a permission tier.</summary>
    public ulong Support { get; set; }
}

/// <summary>
/// Discord channel IDs used by the bot's core features (logging, moderation alerts).
/// Plugins may define additional channels in their own config files.
/// </summary>
public sealed class ChannelsConfig
{
    /// <summary>Channel where the bot sends activity logs and error reports.</summary>
    public ulong LogChannel { get; set; }

    /// <summary>Channel for moderation alerts (spam detection, etc.).</summary>
    public ulong StaffChannel { get; set; }
}
