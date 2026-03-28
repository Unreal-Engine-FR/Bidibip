using System.Collections.Concurrent;
using Bidibip.Plugin.Sdk;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace Bidibip.Plugins.Repost;

[BidibipPlugin]
public sealed class RepostPlugin : IBidibipPlugin
{
    public string Name => "Repost";
    public string Description => "Permet de lier un salon à un forum";

    private ILogger _logger = null!;

    internal static string ConfigPath = null!;

    // Tracks threads already being processed to prevent duplicate handling
    // (Discord fires ThreadCreated twice for forum posts: creation + bot auto-join)
    private readonly ConcurrentDictionary<ulong, byte> _processingThreads = new();

    // Debounced thread renames: coalesces rapid votes into a single rename attempt,
    // retries on rate limit with fresh vote counts from config.
    private static readonly ConcurrentDictionary<ulong, CancellationTokenSource> _pendingRenames = new();

    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".webp", ".avif", ".gif"];
    private static readonly string[] MediaExtensions = [".mp4", ".mov", ".avi", ".mkv", ".flv", ".jpg", ".jpeg", ".png", ".webp", ".avif", ".gif"];

    public async Task InitializeAsync(PluginContext context)
    {
        _logger = context.Logger;
        ConfigPath = Path.Combine(context.DataPath, "config.json");

        await PluginData.LoadAsync<RepostConfig>(ConfigPath);

        context.Events.OnThreadCreated(HandleThreadCreatedAsync);
    }

    internal static async Task<RepostConfig> LoadConfigAsync() =>
        await PluginData.LoadAsync<RepostConfig>(ConfigPath);

    internal static async Task SaveConfigAsync(RepostConfig config) =>
        await PluginData.SaveAsync(ConfigPath, config);

    private async Task HandleThreadCreatedAsync(SocketThreadChannel thread)
    {
        if (thread.Type != ThreadType.PublicThread)
            return;

        var parentId = thread.CategoryId;
        if (parentId is null)
            return;

        var config = await LoadConfigAsync();
        var forumKey = parentId.Value.ToString();

        if (!config.Forums.TryGetValue(forumKey, out var forumConfig))
            return;

        // Prevent duplicate processing (Discord fires ThreadCreated twice for forum posts)
        if (!_processingThreads.TryAdd(thread.Id, 0))
            return;

        var threadKey = thread.Id.ToString();

        // Retry up to 10 times to get the first message (may not be available immediately)
        IMessage? initialMessage = null;
        for (int retry = 0; retry < 10; retry++)
        {
            try
            {
                var messages = await thread.GetMessagesAsync(1).FlattenAsync();
                initialMessage = messages.FirstOrDefault();
                if (initialMessage != null) break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to get first message in thread {Thread}: {Error}", thread.Name, ex.Message);
            }

            await Task.Delay(3000);
        }

        if (initialMessage == null)
        {
            _logger.LogError("Failed to get first message in thread {Thread} after 10 attempts", thread.Mention);
            return;
        }

        // Get thread owner as guild member (use first message author = thread creator)
        var ownerId = initialMessage.Author.Id;
        IGuildUser? owner = thread.Guild.GetUser(ownerId);
        if (owner == null)
        {
            try { owner = await ((IGuild)thread.Guild).GetUserAsync(ownerId); }
            catch { }
        }

        if (owner == null)
        {
            _logger.LogError("Failed to get owner member for thread {Thread}", thread.Name);
            return;
        }

        var forumName = thread.Guild.GetChannel(parentId.Value)?.Name ?? "Unknown";
        var messageUrl = $"https://discord.com/channels/{thread.Guild.Id}/{thread.Id}/{initialMessage.Id}";

        // If voting is enabled, send vote message in thread and init config
        if (forumConfig.VoteEnabled)
        {
            var voteMessage = await thread.SendMessageAsync("Vote en réagissant au post !");
            config.Votes[threadKey] = new VoteConfig
            {
                ThreadName = thread.Name,
                SourceMessageUrl = messageUrl,
                SourceThread = thread.Id,
                VoteMessage = new MessageRef { ChannelId = thread.Id, MessageId = voteMessage.Id },
            };
        }

        // Repost to all destination channels
        foreach (var repostChannelId in forumConfig.RepostChannels)
        {
            var destChannel = thread.Guild.GetTextChannel(repostChannelId);
            if (destChannel == null)
            {
                _logger.LogWarning("Destination channel {ChannelId} not found", repostChannelId);
                continue;
            }

            var sentMessages = await SendRepostMessagesAsync(
                destChannel, initialMessage, messageUrl, thread.Name, forumName,
                owner.DisplayName, owner.GetAvatarUrl() ?? owner.GetDefaultAvatarUrl());

            // Track LAST reposted message for vote buttons (matching Rust thread_create behavior)
            if (forumConfig.VoteEnabled && sentMessages.Count > 0)
            {
                if (config.Votes.TryGetValue(threadKey, out var votes))
                {
                    votes.RepostedMessages.Add(new MessageRef
                    {
                        ChannelId = repostChannelId,
                        MessageId = sentMessages[^1].Id
                    });
                }
            }
        }

        if (forumConfig.VoteEnabled)
        {
            await SaveConfigAsync(config);
            await UpdateVoteMessagesAsync(thread.Guild, thread.Id, config);
        }

        _logger.LogInformation("Auto-reposted thread {ThreadName} ({ThreadId})", thread.Name, thread.Id);
    }

    /// <summary>
    /// Builds and sends the repost messages to a destination channel.
    /// Mirrors the Rust make_repost_message + send logic.
    /// </summary>
    internal static async Task<List<IUserMessage>> SendRepostMessagesAsync(
        ITextChannel destination,
        IMessage sourceMessage,
        string messageUrl,
        string threadName,
        string forumName,
        string authorDisplayName,
        string? authorAvatarUrl)
    {
        var urls = FindUrls(sourceMessage.Content ?? "");
        foreach (var att in sourceMessage.Attachments)
            urls.Add(att.Url);

        // Build embed author
        var embedAuthor = new EmbedAuthorBuilder().WithName(authorDisplayName);
        if (!string.IsNullOrEmpty(authorAvatarUrl))
            embedAuthor.WithIconUrl(authorAvatarUrl);

        // First non-media URL → author URL
        foreach (var url in urls)
        {
            if (!IsMediaUrl(url) && url.StartsWith("http", StringComparison.Ordinal))
            {
                embedAuthor.WithUrl(url);
                break;
            }
        }

        // Build main embed: GREEN color, title = thread name, description = content
        var embed = new EmbedBuilder()
            .WithColor(Color.Green)
            .WithTitle(threadName)
            .WithDescription(Truncate(sourceMessage.Content ?? "", 4096))
            .WithAuthor(embedAuthor);

        // First image URL → embed image (remove from urls list)
        for (int i = 0; i < urls.Count; i++)
        {
            if (IsImageUrl(urls[i]))
            {
                embed.WithImageUrl(urls[i]);
                urls.RemoveAt(i);
                break;
            }
        }

        // Build message list: main message + one per remaining URL
        var messageParts = new List<(string? text, Embed? embed, MessageComponent? components)>
        {
            ($"Nouveau post dans {forumName} : {messageUrl}", embed.Build(), null)
        };

        foreach (var url in urls)
            messageParts.Add((url, null, null));

        // Add "Viens donc voir !" link button to last message
        var linkButton = new ComponentBuilder()
            .WithButton("Viens donc voir !", style: ButtonStyle.Link, url: messageUrl)
            .Build();

        var lastIdx = messageParts.Count - 1;
        var last = messageParts[lastIdx];
        messageParts[lastIdx] = (last.text, last.embed, linkButton);

        // Send all messages
        var sent = new List<IUserMessage>();
        foreach (var (text, emb, comps) in messageParts)
        {
            var msg = await destination.SendMessageAsync(
                text: text,
                embed: emb,
                components: comps);
            sent.Add(msg);
        }

        return sent;
    }

    /// <summary>
    /// Updates vote buttons on the vote message and all reposted messages,
    /// and updates the thread name with vote counts.
    /// </summary>
    internal static async Task UpdateVoteMessagesAsync(SocketGuild guild, ulong threadId, RepostConfig config)
    {
        var threadKey = threadId.ToString();
        if (!config.Votes.TryGetValue(threadKey, out var voteConfig))
            return;

        var yes = voteConfig.Yes.Count;
        var no = voteConfig.No.Count;

        // Schedule debounced thread rename (avoids hitting Discord's 2-per-10min rate limit)
        ScheduleThreadRename(guild, threadId);

        // Vote buttons (for vote message in thread)
        var voteButtons = new ComponentBuilder()
            .WithButton($"Pour \u2705 {yes}", $"repost:vote-yes:{threadId}", ButtonStyle.Success)
            .WithButton($"Contre \u274c {no}", $"repost:vote-no:{threadId}", ButtonStyle.Danger)
            .WithButton("Voir les votes", $"repost:see-votes:{threadId}", ButtonStyle.Secondary)
            .Build();

        // Update vote message
        try
        {
            if (guild.GetChannel(voteConfig.VoteMessage.ChannelId) is ITextChannel voteChannel)
            {
                var voteMsg = await voteChannel.GetMessageAsync(voteConfig.VoteMessage.MessageId);
                if (voteMsg is IUserMessage userVoteMsg)
                    await userVoteMsg.ModifyAsync(m => m.Components = voteButtons);
            }
        }
        catch { }

        // Buttons for reposted messages: link + vote buttons
        var repostButtons = new ComponentBuilder()
            .WithButton("Viens donc voir !", style: ButtonStyle.Link, url: voteConfig.SourceMessageUrl)
            .WithButton($"Pour \u2705 {yes}", $"repost:vote-yes:{threadId}", ButtonStyle.Success)
            .WithButton($"Contre \u274c {no}", $"repost:vote-no:{threadId}", ButtonStyle.Danger)
            .WithButton("Voir les votes", $"repost:see-votes:{threadId}", ButtonStyle.Secondary)
            .Build();

        // Update all reposted messages
        foreach (var reposted in voteConfig.RepostedMessages)
        {
            try
            {
                if (guild.GetChannel(reposted.ChannelId) is ITextChannel channel)
                {
                    var msg = await channel.GetMessageAsync(reposted.MessageId);
                    if (msg is IUserMessage userMsg)
                        await userMsg.ModifyAsync(m => m.Components = repostButtons);
                }
            }
            catch { }
        }
    }

    /// <summary>
    /// Schedules a debounced thread rename. Rapid votes are coalesced into a single rename.
    /// On rate-limit failure, retries with fresh vote counts from config.
    /// </summary>
    private static void ScheduleThreadRename(SocketGuild guild, ulong threadId)
    {
        // Cancel any previous pending rename for this thread (debounce)
        if (_pendingRenames.TryRemove(threadId, out var oldCts))
        {
            oldCts.Cancel();
            oldCts.Dispose();
        }

        var cts = new CancellationTokenSource();
        _pendingRenames[threadId] = cts;

        _ = Task.Run(async () =>
        {
            // Debounce: wait 10s for more votes to settle
            try { await Task.Delay(10_000, cts.Token); }
            catch (TaskCanceledException) { return; }

            for (int attempt = 0; attempt < 5; attempt++)
            {
                if (cts.Token.IsCancellationRequested) return;

                // Always read latest config for current vote counts
                var config = await LoadConfigAsync();
                var threadKey = threadId.ToString();
                if (!config.Votes.TryGetValue(threadKey, out var voteConfig))
                    break;

                var yes = voteConfig.Yes.Count;
                var no = voteConfig.No.Count;
                var status = yes > no ? "\u2705" : "\u274c";
                var newName = $"[{status}{yes}-{no}] {voteConfig.ThreadName}";

                try
                {
                    if (guild.GetChannel(threadId) is SocketThreadChannel threadChannel)
                        await threadChannel.ModifyAsync(t => t.Name = newName);
                    break; // Success
                }
                catch
                {
                    // Rate limited or other error — wait with increasing delay, then retry with fresh counts
                    try { await Task.Delay(60_000 * (attempt + 1), cts.Token); }
                    catch (TaskCanceledException) { return; }
                }
            }

            _pendingRenames.TryRemove(threadId, out _);
        });
    }

    internal static List<string> FindUrls(string text)
    {
        if (string.IsNullOrEmpty(text))
            return [];

        var urls = new List<string>();
        var parts = text.Split([' ', '\t', '\n', '\r', '[', ']', '(', ')'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (part.StartsWith("http", StringComparison.Ordinal))
                urls.Add(part);
        }
        urls.Reverse();
        return urls;
    }

    internal static bool IsImageUrl(string url)
    {
        var path = url.Split('?')[0].Split('#')[0];
        return ImageExtensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }

    internal static bool IsMediaUrl(string url)
    {
        var path = url.Split('?')[0].Split('#')[0];
        return MediaExtensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }

    internal static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length <= maxLength ? text : text[..(maxLength - 3)] + "...";
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
