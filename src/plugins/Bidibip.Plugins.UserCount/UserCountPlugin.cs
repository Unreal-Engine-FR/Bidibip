using Bidibip.Plugin.Sdk;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace Bidibip.Plugins.UserCount;

[BidibipPlugin]
public sealed class UserCountPlugin : IBidibipPlugin
{
    public string Name => "member-count";
    public string Description => "Compte le nombre de membres et l'affiche dans l'activité de Bidibip";

    private ILogger _logger = null!;
    private DiscordSocketClient _client = null!;
    private int _memberCount;

    public Task InitializeAsync(PluginContext context)
    {
        _logger = context.Logger;
        _client = context.Client;

        context.Events.OnBotReady(async () =>
        {
            var guild = _client.GetGuild(context.BotConfig.GuildId);
            if (guild is null) return;

            await guild.DownloadUsersAsync();
            var count = guild.MemberCount;
            Interlocked.Exchange(ref _memberCount, count);
            _logger.LogInformation("There is {Count} users", count);
            UpdateStatus();
        });

        context.Events.OnUserJoined(_ =>
        {
            Interlocked.Increment(ref _memberCount);
            UpdateStatus();
            return Task.CompletedTask;
        });

        context.Events.OnUserLeft((_, _) =>
        {
            Interlocked.Decrement(ref _memberCount);
            UpdateStatus();
            return Task.CompletedTask;
        });

        return Task.CompletedTask;
    }

    private void UpdateStatus()
    {
        var count = Interlocked.CompareExchange(ref _memberCount, 0, 0);
        _client.SetActivityAsync(new CustomStatusGame($"Nous sommes {count} membres"));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
