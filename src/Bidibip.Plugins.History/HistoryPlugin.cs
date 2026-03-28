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
    public string Description => "Historique des messages modifiés et supprimés";

    private HistoryConfig _config = new();
    private BotConfig _botConfig = null!;
    private ILogger _logger = null!;
    private string _configPath = null!;

    public async Task InitializeAsync(PluginContext context)
    {
        _logger = context.Logger;
        _botConfig = context.BotConfig;
        _configPath = Path.Combine(context.DataPath, "config.json");

        await LoadConfigAsync();

        context.Events.OnMessageDeleted(HandleMessageDeletedAsync);
        context.Events.OnMessageUpdated(HandleMessageUpdatedAsync);
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
        }
    }

    private async Task HandleMessageDeletedAsync(Cacheable<IMessage, ulong> cachedMessage, Cacheable<IMessageChannel, ulong> cachedChannel)
    {
        if (_botConfig.Channels.LogChannel == 0)
            return;

        var channel = await cachedChannel.GetOrDownloadAsync();
        if (channel is not SocketTextChannel textChannel)
            return;

        if (_config.ChannelBlacklist.Contains(textChannel.Id))
            return;

        var guild = textChannel.Guild;
        var logChannel = guild.GetTextChannel(_botConfig.Channels.LogChannel);
        if (logChannel is null)
            return;

        var messageId = cachedMessage.Id;
        var date = SnowflakeUtils.FromSnowflake(messageId);
        var dateStr = date.ToString("dd MMMM yyyy");
        var messageLink = $"https://discord.com/channels/{guild.Id}/{textChannel.Id}/{messageId}";

        if (cachedMessage.HasValue)
        {
            var message = cachedMessage.Value;

            // Skip bot's own messages
            if (message.Author.Id == guild.CurrentUser.Id)
                return;

            var content = message.Content;
            if (string.IsNullOrEmpty(content) && message.Attachments.Count > 0)
                content = string.Join(" ", message.Attachments.Select(a => a.Url));
            if (string.IsNullOrEmpty(content))
                content = messageLink;

            var userName = $"{message.Author.Username} ({message.Author.Id})";

            var embed = new EmbedBuilder()
                .WithColor(Color.Red)
                .WithTitle($"Message du {dateStr} supprimé")
                .WithDescription(messageLink)
                .AddField($"de : {userName}", Truncate(content, 1024), false);

            _logger.LogInformation("Message {Link} de {User} du {Date} supprimé : {Content}",
                messageLink, userName, dateStr, content);

            await logChannel.SendMessageAsync(embed: embed.Build());
        }
        else
        {
            var embed = new EmbedBuilder()
                .WithColor(Color.Red)
                .WithTitle($"Ancien message du {dateStr} supprimé")
                .WithDescription(messageLink);

            _logger.LogInformation("Ancien message du {Date} supprimé : {Link}", dateStr, messageLink);

            await logChannel.SendMessageAsync(embed: embed.Build());
        }
    }

    private async Task HandleMessageUpdatedAsync(Cacheable<IMessage, ulong> cachedBefore, IMessage after, IMessageChannel channel)
    {
        if (_botConfig.Channels.LogChannel == 0)
            return;

        if (channel is not SocketTextChannel textChannel)
            return;

        if (_config.ChannelBlacklist.Contains(textChannel.Id))
            return;

        // Skip bot's own messages
        if (after.Author.Id == textChannel.Guild.CurrentUser.Id)
            return;

        var guild = textChannel.Guild;
        var logChannel = guild.GetTextChannel(_botConfig.Channels.LogChannel);
        if (logChannel is null)
            return;

        // Resolve old text
        var oldText = "";
        if (cachedBefore.HasValue)
        {
            oldText = cachedBefore.Value.Content;
            if (string.IsNullOrEmpty(oldText) && cachedBefore.Value.Attachments.Count > 0)
                oldText = string.Join(" ", cachedBefore.Value.Attachments.Select(a => a.Url));
        }

        // Resolve new text
        var newText = after.Content;
        if (string.IsNullOrEmpty(newText) && after.Attachments.Count > 0)
            newText = string.Join(" ", after.Attachments.Select(a => a.Url));

        // Skip if content didn't change (embed updates, etc.)
        if (cachedBefore.HasValue && cachedBefore.Value.Content == after.Content)
            return;

        var messageLink = $"https://discord.com/channels/{guild.Id}/{textChannel.Id}/{after.Id}";
        var userName = $"{after.Author.Username} ({after.Author.Id})";

        var embed = new EmbedBuilder()
            .WithColor(Color.Orange)
            .WithTitle(userName)
            .WithDescription($"Message modifié : {messageLink}");

        if (!string.IsNullOrEmpty(oldText))
            embed.AddField("ancien", Truncate(oldText, 1024), false);

        if (!string.IsNullOrEmpty(newText))
            embed.AddField("nouveau", Truncate(newText, 1024), false);

        _logger.LogInformation("Message de {User} modifié : [[FROM]] {Old} [[TO]] {New}",
            userName, oldText, newText);

        await logChannel.SendMessageAsync(embed: embed.Build());
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
            return value;
        return value[..(maxLength - 3)] + "...";
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
