using System.Text.Json.Serialization;

namespace Bidibip.Plugins.Repost;

public sealed class RepostConfig
{
    [JsonPropertyName("links")]
    public List<ForumLink> Links { get; set; } = [];

    [JsonPropertyName("votes")]
    public Dictionary<string, ThreadVoteData> Votes { get; set; } = new();
}

public sealed class ForumLink
{
    [JsonPropertyName("forum_id")]
    public ulong ForumId { get; set; }

    [JsonPropertyName("destination_id")]
    public ulong DestinationId { get; set; }

    [JsonPropertyName("voting_enabled")]
    public bool VotingEnabled { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
}

public sealed class ThreadVoteData
{
    [JsonPropertyName("yes")]
    public List<string> Yes { get; set; } = [];

    [JsonPropertyName("no")]
    public List<string> No { get; set; } = [];

    [JsonPropertyName("repost_message_id")]
    public ulong RepostMessageId { get; set; }

    [JsonPropertyName("vote_message_id")]
    public ulong VoteMessageId { get; set; }
}
