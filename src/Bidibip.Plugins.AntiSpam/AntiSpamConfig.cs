using System.Text.Json.Serialization;

namespace Bidibip.Plugins.AntiSpam;

public sealed class AntiSpamConfig
{
    [JsonPropertyName("min_occurrences")]
    public int MinOccurrences { get; set; } = 3;

    [JsonPropertyName("max_delay_ms")]
    public int MaxDelayMs { get; set; } = 60000;

    [JsonPropertyName("moderation_channel")]
    public ulong ModerationChannel { get; set; }
}
