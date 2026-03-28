using System.Text.Json.Serialization;

namespace Bidibip.Plugins.AntiSpam;

public sealed class AntiSpamConfig
{
    [JsonPropertyName("min_occurrences")]
    public int MinOccurrences { get; set; } = 3;

    [JsonPropertyName("max_delay_ms")]
    public long MaxDelayMs { get; set; } = 60000;

    [JsonPropertyName("mute_role")]
    public ulong MuteRole { get; set; }

    [JsonPropertyName("moderation_channel")]
    public ulong ModerationChannel { get; set; }

    [JsonPropertyName("spammers")]
    public Dictionary<string, SpammerContext> Spammers { get; set; } = new();
}

public sealed class SpammerContext
{
    [JsonPropertyName("kick_button")]
    public string KickButton { get; set; } = "";

    [JsonPropertyName("pardon_button")]
    public string PardonButton { get; set; } = "";

    [JsonPropertyName("spammer")]
    public ulong Spammer { get; set; }
}
