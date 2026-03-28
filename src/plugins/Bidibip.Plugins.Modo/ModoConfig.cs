using System.Text.Json.Serialization;

namespace Bidibip.Plugins.Modo;

public sealed class ModoConfig
{
    [JsonPropertyName("modo_channel")]
    public ulong ModoChannel { get; set; }

    [JsonPropertyName("tickets")]
    public Dictionary<string, ulong> Tickets { get; set; } = new();
}
