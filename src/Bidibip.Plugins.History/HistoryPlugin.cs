using System.Text.Json;
using Bidibip.Plugin.Sdk;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace Bidibip.Plugins.History;

[BidibipPlugin]
public sealed class HistoryPlugin : IBidibipPlugin
{
    public string Name => "History";
    public string Description => "Tracks message deletions and edits, posts alerts to a configured history channel.";

    private HistoryConfig _config = new();
    private ILogger _logger = null!;
    private string _configPath = null!;

    public async Task InitializeAsync(PluginContext context)
    {
        _logger = context.Logger;
        _configPath = Path.Combine(context.DataPath, "config.json");

        await LoadConfigAsync();

        context.Events.OnMessageDeleted(HandleMessageDeletedAsync);
        context.Events.OnMessageUpdated(HandleMessageUpdatedAsync);

        _logger.LogInformation("History plugin initialized. Tracking channel: {Channel}", _config.HistoryChannel);
    }

    private async Task LoadConfigAsync()
    {
        if (File.Exists(_configPath))
        {
            var json = await File.ReadAllTextAsync(_configPath);
            _config = JsonSerializer.Deserialize<HistoryConfig>(json, PluginJsonOptions.Default) ?? new HistoryConfig();
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
            _config = new HistoryConfig();
            var json = JsonSerializer.Serialize(_config, PluginJsonOptions.Default);
            await File.WriteAllTextAsync(_configPath, json);
            _logger.LogWarning("History config not found, created default at {Path}. Please configure history_channel.", _configPath);
        }
    }

    private async Task HandleMessageDeletedAsync(Cacheable<IMessage, ulong> cachedMessage, Cacheable<IMessageChannel, ulong> cachedChannel)
    {
        if (_config.HistoryChannel == 0)
            return;

        var channel = await cachedChannel.GetOrDownloadAsync();
        if (channel is not SocketTextChannel textChannel)
            return;

        if (_config.ChannelBlacklist.Contains(textChannel.Id))
            return;

        var guild = textChannel.Guild;
        var historyChannel = guild.GetTextChannel(_config.HistoryChannel);
        if (historyChannel is null)
        {
            _logger.LogWarning("History channel {ChannelId} not found in guild.", _config.HistoryChannel);
            return;
        }

        if (cachedMessage.HasValue)
        {
            var message = cachedMessage.Value;

            if (message.Author.IsBot)
                return;

            var embed = new EmbedBuilder()
                .WithColor(Color.Red)
                .WithTitle("Message Deleted")
                .AddField("Author", $"{message.Author.Username} ({message.Author.Id})", inline: true)
                .AddField("Channel", $"<#{textChannel.Id}>", inline: true)
                .AddField("Content", string.IsNullOrEmpty(message.Content) ? "*empty*" : Truncate(message.Content, 1024))
                .WithTimestamp(DateTimeOffset.UtcNow)
                .Build();

            await historyChannel.SendMessageAsync(embed: embed);
        }
        else
        {
            var embed = new EmbedBuilder()
                .WithColor(Color.Red)
                .WithTitle("Message Deleted")
                .AddField("Message ID", cachedMessage.Id.ToString(), inline: true)
                .AddField("Channel", $"<#{textChannel.Id}>", inline: true)
                .AddField("Content", "*message was not cached*")
                .WithTimestamp(DateTimeOffset.UtcNow)
                .Build();

            await historyChannel.SendMessageAsync(embed: embed);
        }
    }

    private async Task HandleMessageUpdatedAsync(Cacheable<IMessage, ulong> cachedBefore, IMessage after, IMessageChannel channel)
    {
        if (_config.HistoryChannel == 0)
            return;

        if (channel is not SocketTextChannel textChannel)
            return;

        if (_config.ChannelBlacklist.Contains(textChannel.Id))
            return;

        if (after.Author.IsBot)
            return;

        var guild = textChannel.Guild;
        var historyChannel = guild.GetTextChannel(_config.HistoryChannel);
        if (historyChannel is null)
        {
            _logger.LogWarning("History channel {ChannelId} not found in guild.", _config.HistoryChannel);
            return;
        }

        var beforeContent = cachedBefore.HasValue
            ? (string.IsNullOrEmpty(cachedBefore.Value.Content) ? "*empty*" : Truncate(cachedBefore.Value.Content, 1024))
            : "*message was not cached*";

        var afterContent = string.IsNullOrEmpty(after.Content) ? "*empty*" : Truncate(after.Content, 1024);

        if (cachedBefore.HasValue && cachedBefore.Value.Content == after.Content)
            return;

        var embed = new EmbedBuilder()
            .WithColor(Color.Orange)
            .WithTitle("Message Edited")
            .AddField("Author", $"{after.Author.Username} ({after.Author.Id})", inline: true)
            .AddField("Channel", $"<#{textChannel.Id}>", inline: true)
            .AddField("Before", beforeContent)
            .AddField("After", afterContent)
            .WithTimestamp(DateTimeOffset.UtcNow)
            .Build();

        await historyChannel.SendMessageAsync(embed: embed);
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
            return value;
        return value[..(maxLength - 3)] + "...";
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
