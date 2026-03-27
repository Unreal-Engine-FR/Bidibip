using System.Text.Json.Serialization;

namespace Bidibip.Plugins.History;

public sealed class HistoryConfig
{
    [JsonPropertyName("history_channel")]
    public ulong HistoryChannel { get; set; }

    [JsonPropertyName("channel_blacklist")]
    public ulong[] ChannelBlacklist { get; set; } = [];
}
