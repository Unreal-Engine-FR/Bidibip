using System.Text.Json.Serialization;

namespace Bidibip.Plugins.FreeForTheMonth;

public sealed class FreeForTheMonthConfig
{
    [JsonPropertyName("channel")]
    public ulong Channel { get; set; }

    [JsonPropertyName("notify_ffm_role")]
    public ulong NotifyFfmRole { get; set; }

    [JsonPropertyName("check_interval_hours")]
    public int CheckIntervalHours { get; set; } = 6;

    /// <summary>Listing UIDs from the last announcement, used to detect new batches.</summary>
    [JsonPropertyName("known_listings")]
    public List<string> KnownListings { get; set; } = [];
}
