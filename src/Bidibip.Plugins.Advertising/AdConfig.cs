using System.Text.Json.Serialization;

namespace Bidibip.Plugins.Advertising;

public sealed class AdConfig
{
    [JsonPropertyName("ad_forum")]
    public ulong AdForum { get; set; }

    [JsonPropertyName("in_progress_channel")]
    public ulong InProgressChannel { get; set; }

    [JsonPropertyName("reviewer_roles")]
    public List<ulong> ReviewerRoles { get; set; } = [];

    [JsonPropertyName("max_ad_per_user")]
    public int MaxAdPerUser { get; set; } = 2;

    /// <summary>Key: threadId (string) -> AdInProgress</summary>
    [JsonPropertyName("in_progress")]
    public Dictionary<string, AdInProgress> InProgress { get; set; } = new();

    /// <summary>Key: userId (string) -> { channelId (string) -> StoredAd }</summary>
    [JsonPropertyName("stored_ads")]
    public Dictionary<string, Dictionary<string, StoredAd>> StoredAds { get; set; } = new();
}

public sealed class StoredAd
{
    [JsonPropertyName("message_channel")]
    public ulong MessageChannel { get; set; }

    [JsonPropertyName("message_id")]
    public ulong MessageId { get; set; }

    [JsonPropertyName("description")]
    public AdInProgress Description { get; set; } = new();
}

public sealed class AdInProgress
{
    [JsonPropertyName("user_id")]
    public ulong UserId { get; set; }

    [JsonPropertyName("thread_id")]
    public ulong ThreadId { get; set; }

    [JsonPropertyName("step")]
    public string Step { get; set; } = "title";

    // ── Main fields ──────────────────────────────────────────────────

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("who_are_you")]
    public string? WhoAreYou { get; set; }

    /// <summary>volunteering, internship, freelance, work_study, fixed_term, open_ended</summary>
    [JsonPropertyName("contract_type")]
    public string? ContractType { get; set; }

    /// <summary>recruiter, worker</summary>
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    /// <summary>discord, other</summary>
    [JsonPropertyName("contact_method")]
    public string? ContactMethod { get; set; }

    [JsonPropertyName("contact_info")]
    public string? ContactInfo { get; set; }

    [JsonPropertyName("other_urls")]
    public string? OtherUrls { get; set; }

    // ── Contract-specific ────────────────────────────────────────────

    [JsonPropertyName("duration")]
    public string? Duration { get; set; }

    [JsonPropertyName("compensation")]
    public string? Compensation { get; set; }

    /// <summary>yes, no (internship only)</summary>
    [JsonPropertyName("has_compensation")]
    public string? HasCompensation { get; set; }

    // ── Recruiter-specific ───────────────────────────────────────────

    /// <summary>remote, flex, on_site</summary>
    [JsonPropertyName("location_type")]
    public string? LocationType { get; set; }

    [JsonPropertyName("location_detail")]
    public string? LocationDetail { get; set; }

    [JsonPropertyName("studio_name")]
    public string? StudioName { get; set; }

    [JsonPropertyName("responsibilities")]
    public string? Responsibilities { get; set; }

    [JsonPropertyName("qualifications")]
    public string? Qualifications { get; set; }

    // ── Worker-specific ──────────────────────────────────────────────

    /// <summary>remote, anywhere, on_site</summary>
    [JsonPropertyName("worker_location_type")]
    public string? WorkerLocationType { get; set; }

    [JsonPropertyName("worker_location_detail")]
    public string? WorkerLocationDetail { get; set; }

    [JsonPropertyName("skills")]
    public string? Skills { get; set; }

    // ── Inline editing ────────────────────────────────────────────────

    /// <summary>Maps step name to the question message ID (for editing the question display).</summary>
    [JsonPropertyName("question_messages")]
    public Dictionary<string, ulong> QuestionMessages { get; set; } = [];

    /// <summary>Track if other_urls was skipped.</summary>
    [JsonPropertyName("other_urls_skipped")]
    public bool OtherUrlsSkipped { get; set; }

    /// <summary>The step currently being edited via modal (set before opening the modal).</summary>
    [JsonPropertyName("editing_step")]
    public string? EditingStep { get; set; }

    // ── Editing ──────────────────────────────────────────────────────

    [JsonPropertyName("edited_post_channel")]
    public ulong? EditedPostChannel { get; set; }

    [JsonPropertyName("edited_post_message")]
    public ulong? EditedPostMessage { get; set; }

    [JsonPropertyName("preview_message_id")]
    public ulong? PreviewMessageId { get; set; }
}
