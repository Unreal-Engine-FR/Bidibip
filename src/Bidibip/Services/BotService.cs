using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bidibip.Services;

public class BotService : IHostedService
{
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _interactions;
    private readonly PluginManager _pluginManager;
    private readonly DiscordLogService _discordLogService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BotService> _logger;

    public BotService(
        DiscordSocketClient client,
        InteractionService interactions,
        PluginManager pluginManager,
        DiscordLogService discordLogService,
        IConfiguration configuration,
        ILogger<BotService> logger)
    {
        _client = client;
        _interactions = interactions;
        _pluginManager = pluginManager;
        _discordLogService = discordLogService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _discordLogService.Initialize(_client, _configuration);

        _client.Log += LogAsync;

        // These handlers must not block the gateway thread.
        // Return Task.CompletedTask immediately and do work on background threads.
        _client.Ready += () =>
        {
            _ = Task.Run(OnReadyAsync);
            return Task.CompletedTask;
        };

        _client.InteractionCreated += interaction =>
        {
            _ = Task.Run(() => HandleInteractionAsync(interaction));
            return Task.CompletedTask;
        };

        _interactions.Log += LogAsync;
        _interactions.SlashCommandExecuted += SlashCommandExecutedAsync;

        var token = _configuration["Discord:Token"]
            ?? throw new InvalidOperationException("Discord:Token is not configured. Set it in appsettings.json or via DISCORD__TOKEN environment variable.");

        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _client.StopAsync();
    }

    private async Task OnReadyAsync()
    {
        _logger.LogInformation("Bot is connected as {User}", _client.CurrentUser);
        _discordLogService.MarkReady();
        await _pluginManager.RegisterCommandsAsync();
    }

    private async Task HandleInteractionAsync(SocketInteraction interaction)
    {
        var commandName = interaction is SocketSlashCommand slash ? slash.CommandName : "N/A";
        _logger.LogInformation("²Interaction received: {Type} {Name}", interaction.Type, commandName);

        // Auto-defer slash commands so handlers can use FollowupAsync.
        // User context menu commands are NOT auto-deferred so handlers can respond with modals.
        // Specific slash commands that need modals (e.g. "sanction") are also excluded.
        if (interaction is SocketSlashCommand slashCmd && slashCmd.CommandName != "sanction")
        {
            try
            {
                await interaction.DeferAsync(ephemeral: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to defer interaction {Name}", commandName);
                return;
            }
        }

        var ctx = new SocketInteractionContext(_client, interaction);

        try
        {
            var result = await _interactions.ExecuteCommandAsync(ctx, services: _pluginManager.ServiceProvider);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Interaction failed: {Error} ({ErrorReason})", result.Error, result.ErrorReason);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception executing interaction {Name}", commandName);
        }
    }

    private Task SlashCommandExecutedAsync(SlashCommandInfo command, IInteractionContext context, IResult result)
    {
        if (!result.IsSuccess)
        {
            _logger.LogError("Slash command /{Command} failed: {Error} ({Reason})",
                command.Name, result.Error, result.ErrorReason);
        }

        return Task.CompletedTask;
    }

    private Task LogAsync(LogMessage msg)
    {
        var level = msg.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Debug,
            LogSeverity.Debug => LogLevel.Trace,
            _ => LogLevel.Information
        };

        _logger.Log(level, msg.Exception, "[Discord] {Message}", msg.Message ?? msg.Exception?.Message ?? "Unknown");
        return Task.CompletedTask;
    }
}
