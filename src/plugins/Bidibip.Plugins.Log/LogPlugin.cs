using System.Text.Json;
using Bidibip.Plugin.Sdk;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace Bidibip.Plugins.Log;

[BidibipPlugin]
public sealed class LogPlugin : IBidibipPlugin
{
    public string Name => "Log";
    public string Description => "logs du serveur dans un channel dédié";

    private ILogger _logger = null!;
    private LogConfig _config = new();

    public async Task InitializeAsync(PluginContext context)
    {
        _logger = context.Logger;

        var configPath = Path.Combine(context.DataPath, "config.json");
        if (File.Exists(configPath))
        {
            var json = await File.ReadAllTextAsync(configPath);
            _config = JsonSerializer.Deserialize<LogConfig>(json, PluginJsonOptions.Default) ?? new LogConfig();
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            var json = JsonSerializer.Serialize(_config, PluginJsonOptions.Default);
            await File.WriteAllTextAsync(configPath, json);
        }

        context.Events.OnUserJoined(user =>
        {
            _logger.LogInformation("{User} a rejoint le serveur", user.Username);
            return Task.CompletedTask;
        });

        context.Events.OnUserLeft((_, user) =>
        {
            _logger.LogInformation("{User} a quitté le serveur", user.GlobalName ?? user.Username);
            return Task.CompletedTask;
        });

        context.Events.OnInteractionCreated(interaction =>
        {
            if (interaction.ChannelId.HasValue && _config.ChannelBlacklist.Contains(interaction.ChannelId.Value))
                return Task.CompletedTask;

            LogInteraction(interaction);
            return Task.CompletedTask;
        });
    }

    private void LogInteraction(SocketInteraction interaction)
    {
        var user = interaction.User.Username;

        switch (interaction)
        {
            case SocketSlashCommand slashCommand:
            {
                var options = FormatOptions(slashCommand.Data.Options);
                if (string.IsNullOrEmpty(options))
                    _logger.LogInformation("User {User} sent command {Command}", user, slashCommand.Data.Name);
                else
                    _logger.LogInformation("User {User} sent command {Command} with options {Options}", user, slashCommand.Data.Name, options);
                break;
            }
            case SocketUserCommand userCommand:
            {
                var targetName = userCommand.Data.Member?.Username ?? "unknown";
                _logger.LogInformation("User {User} sent command {Command} with options cible = {Target}, ", user, userCommand.Data.Name, targetName);
                break;
            }
            case SocketMessageComponent component:
            {
                _logger.LogInformation("User {User} clicked on button {ButtonId}", user, component.Data.CustomId);
                break;
            }
            case SocketModal modal:
            {
                _logger.LogInformation("User {User} sent modal #{ModalId}", user, modal.Data.CustomId);
                break;
            }
        }
    }

    private static string FormatOptions(IReadOnlyCollection<SocketSlashCommandDataOption> options)
    {
        if (options.Count == 0)
            return "";

        var result = "";
        foreach (var opt in options)
        {
            var value = opt.Value switch
            {
                IUser u => u.Username,
                IChannel c => c.Name ?? "Unknown",
                IRole r => $"<@&{r.Id}>",
                IAttachment a => a.Url,
                _ => opt.Value?.ToString() ?? "?"
            };
            result += $"{opt.Name} = {value}, ";
        }
        return result;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
