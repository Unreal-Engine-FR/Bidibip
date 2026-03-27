using System.Text.Json;
using Bidibip.Plugin.Sdk;
using Discord;
using Microsoft.Extensions.Logging;

namespace Bidibip.Plugins.Repost;

[BidibipPlugin]
public sealed class RepostPlugin : IBidibipPlugin
{
    public string Name => "Repost";
    public string Description => "Links forum channels to text channels, reposting new forum threads automatically.";

    private ILogger _logger = null!;

    internal static string ConfigPath = null!;
    internal static readonly object ConfigLock = new();
    internal static readonly JsonSerializerOptions JsonOptions = PluginJsonOptions.Default;

    public async Task InitializeAsync(PluginContext context)
    {
        _logger = context.Logger;
        ConfigPath = Path.Combine(context.DataPath, "config.json");

        await EnsureConfigAsync();

        context.Events.OnMessageReceived(HandleMessageReceivedAsync);

        _logger.LogInformation("Repost plugin initialized.");
    }

    private async Task EnsureConfigAsync()
    {
        if (File.Exists(ConfigPath))
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        var config = new RepostConfig();
        var json = JsonSerializer.Serialize(config, JsonOptions);
        await File.WriteAllTextAsync(ConfigPath, json);
        _logger.LogWarning("Repost config not found, created default at {Path}.", ConfigPath);
    }

    internal static async Task<RepostConfig> LoadConfigAsync()
    {
        if (!File.Exists(ConfigPath))
            return new RepostConfig();

        var json = await File.ReadAllTextAsync(ConfigPath);
        return JsonSerializer.Deserialize<RepostConfig>(json, JsonOptions) ?? new RepostConfig();
    }

    internal static async Task SaveConfigAsync(RepostConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        var json = JsonSerializer.Serialize(config, JsonOptions);
        await File.WriteAllTextAsync(ConfigPath, json);
    }

    private async Task HandleMessageReceivedAsync(IMessage message)
    {
        if (message.Author.IsBot)
            return;

        // Check if the message is in a thread channel
        if (message.Channel is not IThreadChannel thread)
            return;

        var parentId = thread.CategoryId; // ParentChannelId for threads is the forum channel
        if (parentId is null)
            return;

        // Load config and check for a matching forum link
        var config = await LoadConfigAsync();
        var link = config.Links.FirstOrDefault(l =>
            l.Enabled && l.ForumId == parentId.Value);

        if (link is null)
            return;

        // Check if this is the first message in the thread
        // The thread starter message has the same ID as the thread itself
        if (message.Id != thread.Id)
            return;

        try
        {
            var guild = ((IGuildChannel)thread).Guild;
            var destinationChannel = await guild.GetTextChannelAsync(link.DestinationId);
            if (destinationChannel is null)
            {
                _logger.LogWarning("Destination channel {ChannelId} not found for forum link", link.DestinationId);
                return;
            }

            // Build the repost embed
            var embedBuilder = new EmbedBuilder()
                .WithColor(Color.Blue)
                .WithAuthor(message.Author.Username, message.Author.GetAvatarUrl() ?? message.Author.GetDefaultAvatarUrl())
                .WithTitle(thread.Name)
                .WithTimestamp(message.Timestamp);

            // Add message content (truncated if needed)
            if (!string.IsNullOrWhiteSpace(message.Content))
            {
                var content = message.Content.Length > 2048
                    ? message.Content[..2045] + "..."
                    : message.Content;
                embedBuilder.WithDescription(content);
            }

            // Add the first image attachment if present
            var imageAttachment = message.Attachments
                .FirstOrDefault(a => a.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true);
            if (imageAttachment is not null)
            {
                embedBuilder.WithImageUrl(imageAttachment.Url);
            }

            // Build the "View thread" button
            var threadUrl = $"https://discord.com/channels/{guild.Id}/{thread.Id}";
            var components = new ComponentBuilder()
                .WithButton("Voir le fil", style: ButtonStyle.Link, url: threadUrl)
                .Build();

            var repostMessage = await destinationChannel.SendMessageAsync(embed: embedBuilder.Build(), components: components);

            // If voting is enabled, add vote buttons
            if (link.VotingEnabled)
            {
                var threadId = thread.Id.ToString();

                var voteComponents = new ComponentBuilder()
                    .WithButton($"Oui (0)", $"repost::vote_yes::{thread.Id}", ButtonStyle.Success)
                    .WithButton($"Non (0)", $"repost::vote_no::{thread.Id}", ButtonStyle.Danger)
                    .Build();

                var voteMessage = await destinationChannel.SendMessageAsync("Votez :", components: voteComponents);

                // Save vote tracking data
                config.Votes[threadId] = new ThreadVoteData
                {
                    RepostMessageId = repostMessage.Id,
                    VoteMessageId = voteMessage.Id
                };
                await SaveConfigAsync(config);
            }

            _logger.LogInformation("Reposted thread {ThreadName} ({ThreadId}) to channel {ChannelId}",
                thread.Name, thread.Id, link.DestinationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to repost thread {ThreadId} to destination", thread.Id);
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
