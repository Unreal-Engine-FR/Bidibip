using System.Text.Json.Serialization;

namespace Bidibip.Plugins.Warn;

public sealed class WarnData
{
    [JsonPropertyName("moderation_channel")]
    public ulong ModerationChannel { get; set; }

    [JsonPropertyName("warns")]
    public Dictionary<string, List<WarnRecord>> Warns { get; set; } = new();
}

public sealed class WarnRecord
{
    [JsonPropertyName("date")]
    public DateTime Date { get; set; }

    [JsonPropertyName("from_user")]
    public string FromUser { get; set; } = "";

    [JsonPropertyName("from_id")]
    public ulong FromId { get; set; }

    [JsonPropertyName("to_user")]
    public string ToUser { get; set; } = "";

    [JsonPropertyName("to_id")]
    public ulong ToId { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = "";

    [JsonPropertyName("details")]
    public string? Details { get; set; }

    [JsonPropertyName("link")]
    public string? Link { get; set; }

    [JsonPropertyName("action")]
    public string Action { get; set; } = "warn";

    [JsonPropertyName("full_message_link")]
    public string FullMessageLink { get; set; } = "";
}
