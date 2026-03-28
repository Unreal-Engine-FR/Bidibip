namespace Bidibip.Plugin.Sdk.Permissions;

/// <summary>
/// Defines the role tiers for command access control.
/// Higher values grant more privilege. A command tagged with a given role
/// is accessible to that role AND all roles above it.
/// </summary>
public enum BotRole
{
    /// <summary>Any user, no role required.</summary>
    Everyone = 0,

    /// <summary>Verified member of the server.</summary>
    Member = 1,

    /// <summary>Helper staff.</summary>
    Helper = 2,

    /// <summary>Moderator.</summary>
    Moderator = 3,

    /// <summary>Full administrator (default for all commands).</summary>
    Administrator = 4,
}
