using System.Text.Json.Serialization;

namespace Bidibip.Plugins.Log;

public sealed class LogConfig
{
    [JsonPropertyName("channel_blacklist")]
    public ulong[] ChannelBlacklist { get; set; } = [];
}
