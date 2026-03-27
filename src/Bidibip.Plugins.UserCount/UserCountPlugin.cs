using Bidibip.Plugin.Sdk;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace Bidibip.Plugins.UserCount;

[BidibipPlugin]
public sealed class UserCountPlugin : IBidibipPlugin
{
    public string Name => "UserCount";
    public string Description => "Tracks guild member count and displays it as the bot's custom status.";

    private ILogger _logger = null!;
    private int _memberCount;
    private bool _initialized;

    public Task InitializeAsync(PluginContext context)
    {
        _logger = context.Logger;

        context.Events.OnUserJoined(async user =>
        {
            if (!_initialized && user is SocketGuildUser sgu)
                await InitializeMemberCountAsync(sgu.Guild);

            Interlocked.Increment(ref _memberCount);
            if (user is SocketGuildUser su)
                await UpdateStatusAsync(su.Guild);
        });

        context.Events.OnUserLeft(async (guild, _) =>
        {
            if (!_initialized && guild is SocketGuild sg)
                await InitializeMemberCountAsync(sg);

            Interlocked.Decrement(ref _memberCount);
            if (guild is SocketGuild sg2)
                await UpdateStatusAsync(sg2);
        });

        _logger.LogInformation("UserCount plugin initialized");
        return Task.CompletedTask;
    }

    private async Task InitializeMemberCountAsync(SocketGuild guild)
    {
        await guild.DownloadUsersAsync();
        Interlocked.Exchange(ref _memberCount, guild.MemberCount);
        _initialized = true;
        _logger.LogInformation("Initialized member count: {Count}", _memberCount);
        await UpdateStatusAsync(guild);
    }

    private async Task UpdateStatusAsync(SocketGuild guild)
    {
        var count = Interlocked.CompareExchange(ref _memberCount, 0, 0);
        var client = (guild as IGuild).GetType().GetProperty("Discord",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(guild) as DiscordSocketClient;

        if (client is not null)
            await client.SetActivityAsync(new Game($"Nous sommes {count} membres", ActivityType.CustomStatus));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
