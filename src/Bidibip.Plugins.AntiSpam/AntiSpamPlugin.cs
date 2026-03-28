using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bidibip.Plugin.Sdk;
using Discord;
using Microsoft.Extensions.Logging;

namespace Bidibip.Plugins.AntiSpam;

[BidibipPlugin]
public sealed class AntiSpamPlugin : IBidibipPlugin
{
    public string Name => "AntiSpam";
    public string Description => "Detects users posting the same message across multiple channels within a time window.";

    private ILogger _logger = null!;
    private string _configPath = null!;
    private AntiSpamConfig _config = new();

    internal static readonly ConcurrentDictionary<ulong, List<(string ContentHash, ulong ChannelId, DateTime Time)>> RecentMessages = new();
    internal static readonly ConcurrentDictionary<ulong, List<(ulong ChannelId, ulong MessageId)>> SpamMessages = new();

    public async Task InitializeAsync(PluginContext context)
    {
        _logger = context.Logger;
        _configPath = Path.Combine(context.DataPath, "config.json");

        await LoadConfigAsync();

        context.Events.OnMessageReceived(HandleMessageReceivedAsync);
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
            _logger.LogWarning("AntiSpam config not found, created default at {Path}. Please configure moderation_channel.", _configPath);
        }
    }

    private async Task HandleMessageReceivedAsync(IMessage message)
    {
        if (message.Author.IsBot)
            return;

        if (message.Channel is not ITextChannel textChannel)
            return;

        if (_config.ModerationChannel != 0 && textChannel.Id == _config.ModerationChannel)
            return;

        if (string.IsNullOrWhiteSpace(message.Content))
            return;

        var userId = message.Author.Id;
        var contentHash = ComputeHash(message.Content);
        var now = DateTime.UtcNow;
        var cutoff = now.AddMilliseconds(-_config.MaxDelayMs);

        var entries = RecentMessages.GetOrAdd(userId, _ => new List<(string, ulong, DateTime)>());

        lock (entries)
        {
            // Clean up old entries
            entries.RemoveAll(e => e.Time < cutoff);

            // Add the current message
            entries.Add((contentHash, textChannel.Id, now));

            // Check how many distinct channels have the same content hash
            var distinctChannels = entries
                .Where(e => e.ContentHash == contentHash)
                .Select(e => e.ChannelId)
                .Distinct()
                .ToList();

            if (distinctChannels.Count < _config.MinOccurrences)
                return;

            // Collect message IDs for deletion before clearing
            var spamEntries = entries
                .Where(e => e.ContentHash == contentHash)
                .Select(e => (e.ChannelId, MessageId: 0ul))
                .ToList();

            // Remove matched entries to avoid re-triggering
            entries.RemoveAll(e => e.ContentHash == contentHash);
        }

        // Spam detected - handle asynchronously outside the lock
        await HandleSpamDetectedAsync(message, textChannel, contentHash);
    }

    private async Task HandleSpamDetectedAsync(IMessage message, ITextChannel sourceChannel, string contentHash)
    {
        var userId = message.Author.Id;

        try
        {
            // Apply timeout
            if (message.Author is IGuildUser guildUser)
            {
                await guildUser.SetTimeOutAsync(TimeSpan.FromMinutes(5));
                _logger.LogInformation("Applied 5-minute timeout to user {User} ({UserId}) for cross-channel spam",
                    message.Author.Username, userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply timeout to user {UserId}", userId);
        }

        // Collect channels where the spam was posted for deletion and reporting
        List<(string ContentHash, ulong ChannelId, DateTime Time)> snapshot;
        var entries = RecentMessages.GetOrAdd(userId, _ => new List<(string, ulong, DateTime)>());
        lock (entries)
        {
            snapshot = entries.Where(e => e.ContentHash == contentHash).ToList();
        }

        // Try to delete the triggering message and recent spam messages
        var spamChannelIds = new HashSet<ulong> { sourceChannel.Id };
        try
        {
            await sourceChannel.DeleteMessageAsync(message.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete spam message {MessageId} in channel {ChannelId}", message.Id, sourceChannel.Id);
        }

        // Delete from other channels where the same content was posted
        if (sourceChannel.Guild is not null)
        {
            foreach (var entry in snapshot)
            {
                spamChannelIds.Add(entry.ChannelId);
            }

            // Search recent messages in the affected channels to find and delete them
            foreach (var channelId in spamChannelIds)
            {
                if (channelId == sourceChannel.Id)
                    continue;

                try
                {
                    var channel = await sourceChannel.Guild.GetTextChannelAsync(channelId);
                    if (channel is null) continue;

                    var recentMsgs = await channel.GetMessagesAsync(20).FlattenAsync();
                    foreach (var msg in recentMsgs)
                    {
                        if (msg.Author.Id == userId && ComputeHash(msg.Content) == contentHash)
                        {
                            await channel.DeleteMessageAsync(msg.Id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete spam messages in channel {ChannelId}", channelId);
                }
            }
        }

        // Send alert to moderation channel
        if (_config.ModerationChannel == 0)
            return;

        try
        {
            var guild = sourceChannel.Guild;
            if (guild is null)
                return;
            var modChannel = await guild.GetTextChannelAsync(_config.ModerationChannel);
            if (modChannel is null)
            {
                _logger.LogWarning("Moderation channel {ChannelId} not found", _config.ModerationChannel);
                return;
            }

            var channelMentions = string.Join(", ", spamChannelIds.Select(id => $"<#{id}>"));
            var spamContent = message.Content.Length > 1024
                ? message.Content[..1021] + "..."
                : message.Content;

            var embed = new EmbedBuilder()
                .WithColor(Color.Red)
                .WithTitle("Spam detecte")
                .AddField("Utilisateur", $"{message.Author.Mention} (`{message.Author.Id}`)", inline: true)
                .AddField("Canaux", channelMentions, inline: true)
                .AddField("Contenu", spamContent)
                .WithTimestamp(DateTimeOffset.UtcNow)
                .Build();

            var components = new ComponentBuilder()
                .WithButton("Kick", $"antispam::kick::{userId}", ButtonStyle.Danger)
                .WithButton("Pardon", $"antispam::pardon::{userId}", ButtonStyle.Success)
                .Build();

            await modChannel.SendMessageAsync(embed: embed, components: components);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send spam alert to moderation channel");
        }
    }

    private static string ComputeHash(string content)
    {
        var normalized = content.Trim().ToLowerInvariant();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes);
    }

    public ValueTask DisposeAsync()
    {
        RecentMessages.Clear();
        SpamMessages.Clear();
        return ValueTask.CompletedTask;
    }
}
