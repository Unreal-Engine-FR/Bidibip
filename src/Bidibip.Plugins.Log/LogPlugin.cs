using Bidibip.Plugin.Sdk;
using Discord;
using Microsoft.Extensions.Logging;

namespace Bidibip.Plugins.Log;

[BidibipPlugin]
public sealed class LogPlugin : IBidibipPlugin
{
    public string Name => "Log";
    public string Description => "Logs guild events (joins, leaves, messages) to the console via ILogger.";

    private ILogger _logger = null!;

    public Task InitializeAsync(PluginContext context)
    {
        _logger = context.Logger;

        context.Events.OnUserJoined(user =>
        {
            _logger.LogInformation("User {DisplayName} | {Username} joined the server",
                user.DisplayName, user.Username);
            return Task.CompletedTask;
        });

        context.Events.OnUserLeft((guild, user) =>
        {
            _logger.LogInformation("User {DisplayName} | {Username} left the server",
                user.GlobalName ?? user.Username, user.Username);
            return Task.CompletedTask;
        });

        context.Events.OnMessageReceived(msg =>
        {
            if (msg.Author.IsBot) return Task.CompletedTask;

            _logger.LogInformation("Message from {Username} in #{Channel}: {Content}",
                msg.Author.Username,
                msg.Channel.Name,
                msg.Content.Length > 100 ? msg.Content[..100] + "..." : msg.Content);

            return Task.CompletedTask;
        });

        _logger.LogInformation("Log plugin initialized");
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
