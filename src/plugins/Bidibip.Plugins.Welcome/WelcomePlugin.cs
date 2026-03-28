using Bidibip.Plugin.Sdk;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace Bidibip.Plugins.Welcome;

[BidibipPlugin]
public sealed class WelcomePlugin : IBidibipPlugin
{
    public string Name => "Welcome";
    public string Description => "Sends welcome and leave messages in configured channels.";

    private static readonly Random Rng = new();

    private ILogger _logger = null!;
    private WelcomeConfig _config = null!;
    private string _configPath = null!;

    public async Task InitializeAsync(PluginContext context)
    {
        _logger = context.Logger;
        _configPath = Path.Combine(context.DataPath, "config.json");

        _config = await PluginData.LoadAsync<WelcomeConfig>(_configPath);

        context.Events.OnUserJoined(async user =>
        {
            _logger.LogInformation("{User} a rejoint le serveur", user.GlobalName ?? user.Username);

            if (_config.JoinChannel == 0) return;

            var guild = user is SocketGuildUser sgu ? sgu.Guild : null;
            var channel = guild?.GetTextChannel(_config.JoinChannel);
            if (channel is null) return;

            var template = _config.WelcomeMessages.Length > 0
                ? _config.WelcomeMessages[Rng.Next(_config.WelcomeMessages.Length)]
                : "Bienvenue parmi nous {user} :wave: !";

            var reglementMention = _config.ReglementChannel != 0
                ? $"<#{_config.ReglementChannel}>"
                : "#reglement";

            var message = template
                .Replace("{user}", user.Mention)
                .Replace("{reglement}", reglementMention);

            message += $"\n> N'oublies pas de lire le {reglementMention} pour accéder au serveur.";

            if (message.Length > 2000)
                message = message[..2000];

            await channel.SendMessageAsync(message);
        });

        context.Events.OnUserLeft(async (guild, user) =>
        {
            _logger.LogInformation("{User} a quitté le serveur", user.GlobalName ?? user.Username);

            if (_config.LeaveChannel == 0) return;

            var socketGuild = guild as SocketGuild;
            var channel = socketGuild?.GetTextChannel(_config.LeaveChannel);
            if (channel is null) return;

            var template = _config.LeaveMessages.Length > 0
                ? _config.LeaveMessages[Rng.Next(_config.LeaveMessages.Length)]
                : "{user} nous a quitté !";

            var message = template.Replace("{user}", $"<@{user.Id}>");

            if (message.Length > 2000)
                message = message[..2000];

            await channel.SendMessageAsync(message);
        });
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
