using System.Text.Json.Serialization;

namespace Bidibip.Plugins.Repost;

public sealed class RepostConfig
{
    [JsonPropertyName("forums")]
    public Dictionary<string, ForumConfig> Forums { get; set; } = new();

    [JsonPropertyName("votes")]
    public Dictionary<string, VoteConfig> Votes { get; set; } = new();
}

public sealed class ForumConfig
{
    [JsonPropertyName("repost_channels")]
    public HashSet<ulong> RepostChannels { get; set; } = [];

    [JsonPropertyName("vote_enabled")]
    public bool VoteEnabled { get; set; }
}

public sealed class VoteConfig
{
    [JsonPropertyName("thread_name")]
    public string ThreadName { get; set; } = "";

    [JsonPropertyName("source_message_url")]
    public string SourceMessageUrl { get; set; } = "";

    [JsonPropertyName("source_thread")]
    public ulong SourceThread { get; set; }

    [JsonPropertyName("reposted_messages")]
    public List<MessageRef> RepostedMessages { get; set; } = [];

    [JsonPropertyName("vote_message")]
    public MessageRef VoteMessage { get; set; } = new();

    [JsonPropertyName("yes")]
    public Dictionary<string, string> Yes { get; set; } = new();

    [JsonPropertyName("no")]
    public Dictionary<string, string> No { get; set; } = new();
}

public sealed class MessageRef
{
    [JsonPropertyName("channel_id")]
    public ulong ChannelId { get; set; }

    [JsonPropertyName("message_id")]
    public ulong MessageId { get; set; }
}
