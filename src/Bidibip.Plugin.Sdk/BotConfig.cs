namespace Bidibip.Plugin.Sdk;

public sealed class BotConfig
{
    public ulong GuildId { get; set; }
    public RolesConfig Roles { get; set; } = new();
    public ChannelsConfig Channels { get; set; } = new();
}

public sealed class RolesConfig
{
    public ulong Administrator { get; set; }
    public ulong Moderator { get; set; }
    public ulong Helper { get; set; }
    public ulong Member { get; set; }
    public ulong Mute { get; set; }
}

public sealed class ChannelsConfig
{
    public ulong LogChannel { get; set; }
    public ulong StaffChannel { get; set; }
}
