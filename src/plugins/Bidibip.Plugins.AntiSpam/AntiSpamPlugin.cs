using System.Collections.Concurrent;
using System.Text.Json;
using Bidibip.Plugin.Sdk;
using Discord;

using Microsoft.Extensions.Logging;

namespace Bidibip.Plugins.AntiSpam;

[BidibipPlugin]
public sealed class AntiSpamPlugin : IBidibipPlugin
{
    public string Name => "AntiSpam";
    public string Description => "Protection contre les spams potentiels";

    private ILogger _logger = null!;
    private string _configPath = null!;
    private AntiSpamConfig _config = new();
    private readonly object _configLock = new();

    internal static string DataPath { get; private set; } = "";

    private sealed class LastMessage
    {
        public string Content { get; set; } = "";
        public List<(DateTime Time, ulong ChannelId, ulong MessageId)> Occurrences { get; set; } = [];
        public bool Warned { get; set; }
    }

    private static readonly ConcurrentDictionary<ulong, LastMessage> History = new();

    public async Task InitializeAsync(PluginContext context)
    {
        _logger = context.Logger;
        DataPath = context.DataPath;
        _configPath = Path.Combine(context.DataPath, "config.json");

        await LoadConfigAsync();

        context.Events.OnMessageReceived(HandleMessageAsync);
    }

    private async Task HandleMessageAsync(IMessage message)
    {
        if (message.Author.IsBot)
            return;

        if (message.Channel is not ITextChannel)
            return;

        if (string.IsNullOrWhiteSpace(message.Content))
            return;

        var userId = message.Author.Id;
        var entry = History.GetOrAdd(userId, _ => new LastMessage());

        List<(ulong ChannelId, ulong MessageId)>? spamMessages = null;

        lock (entry)
        {
            if (entry.Content == message.Content)
            {
                // Same message sent in the same channel = ignore
                foreach (var (_, channelId, _) in entry.Occurrences)
                {
                    if (channelId == message.Channel.Id)
                        return;
                }

                // Collect messages within the time window
                var cutoff = DateTime.UtcNow.AddMilliseconds(-_config.MaxDelayMs);
                var recentMessages = entry.Occurrences
                    .Where(o => o.Time >= cutoff)
                    .Select(o => (o.ChannelId, o.MessageId))
                    .ToList();

                // Add current message to occurrences
                entry.Occurrences.Add((DateTime.UtcNow, message.Channel.Id, message.Id));

                // Check if we reached the spam threshold
                if (recentMessages.Count >= _config.MinOccurrences)
                {
                    if (entry.Warned)
                        return;

                    entry.Warned = true;
                    // Include the current triggering message
                    recentMessages.Add((message.Channel.Id, message.Id));
                    spamMessages = recentMessages;
                }
            }
            else
            {
                // Different message: reset tracking
                entry.Content = message.Content;
                entry.Occurrences.Clear();
                entry.Warned = false;
                entry.Occurrences.Add((DateTime.UtcNow, message.Channel.Id, message.Id));
            }
        }

        if (spamMessages is not null)
            await HandleSpamDetectedAsync(message, spamMessages);
    }

    private async Task HandleSpamDetectedAsync(IMessage message, List<(ulong ChannelId, ulong MessageId)> spamMessages)
    {
        var userId = message.Author.Id;

        // Apply mute role
        try
        {
            if (message.Author is IGuildUser guildUser && _config.MuteRole != 0)
            {
                await guildUser.AddRoleAsync(_config.MuteRole);
                _logger.LogInformation("Muted user {User} ({UserId}) for potential spam",
                    message.Author.Username, userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mute potential spammer {UserId}", userId);
        }

        // Delete all spam messages
        foreach (var (channelId, messageId) in spamMessages)
        {
            try
            {
                if (message.Channel is ITextChannel textChannel && textChannel.Guild is not null)
                {
                    var channel = await textChannel.Guild.GetTextChannelAsync(channelId);
                    if (channel is not null)
                        await channel.DeleteMessageAsync(messageId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete spam message {MessageId} in channel {ChannelId}",
                    messageId, channelId);
            }
        }

        // Send alert to moderation channel
        if (_config.ModerationChannel == 0)
            return;

        try
        {
            if (message.Channel is not ITextChannel srcChannel || srcChannel.Guild is null)
                return;

            var modChannel = await srcChannel.Guild.GetTextChannelAsync(_config.ModerationChannel);
            if (modChannel is null)
            {
                _logger.LogWarning("Moderation channel {ChannelId} not found", _config.ModerationChannel);
                return;
            }

            var kickButtonId = $"antispam_kick_{Guid.NewGuid():N}";
            var pardonButtonId = $"antispam_pardon_{Guid.NewGuid():N}";

            var components = new ComponentBuilder()
                .WithButton("Kick", kickButtonId, ButtonStyle.Danger)
                .WithButton("Pardonner", pardonButtonId, ButtonStyle.Success)
                .Build();

            var modoMessage = await modChannel.SendMessageAsync(
                $"@everyone Spam potentiel de {message.Author.Mention} : `{message.Content}`",
                components: components,
                allowedMentions: new AllowedMentions(AllowedMentionTypes.Everyone | AllowedMentionTypes.Users));

            // Save spammer context
            lock (_configLock)
            {
                _config.Spammers[modoMessage.Id.ToString()] = new SpammerContext
                {
                    KickButton = kickButtonId,
                    PardonButton = pardonButtonId,
                    Spammer = userId
                };
            }

            await SaveConfigAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send spam alert to moderation channel");
        }
    }

    internal static void ClearUserHistory(ulong userId)
    {
        History.TryRemove(userId, out _);
    }

    internal async Task SaveConfigAsync()
    {
        AntiSpamConfig snapshot;
        lock (_configLock)
        {
            snapshot = _config;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
        var json = JsonSerializer.Serialize(snapshot, PluginJsonOptions.Default);
        await File.WriteAllTextAsync(_configPath, json);
    }

    private async Task LoadConfigAsync()
    {
        if (File.Exists(_configPath))
        {
            var json = await File.ReadAllTextAsync(_configPath);
            _config = JsonSerializer.Deserialize<AntiSpamConfig>(json, PluginJsonOptions.Default) ?? new AntiSpamConfig();
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
            _config = new AntiSpamConfig();
            var json = JsonSerializer.Serialize(_config, PluginJsonOptions.Default);
            await File.WriteAllTextAsync(_configPath, json);
            _logger.LogWarning("AntiSpam config not found, created default at {Path}. Please configure moderation_channel and mute_role.", _configPath);
        }
    }

    public ValueTask DisposeAsync()
    {
        History.Clear();
        return ValueTask.CompletedTask;
    }
}
