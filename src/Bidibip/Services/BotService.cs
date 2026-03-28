using Bidibip.Plugin.Sdk;
using Bidibip.Plugin.Sdk.Permissions;
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
    private readonly BotConfig _botConfig;

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

        _botConfig = new BotConfig();
        configuration.GetSection("Bot").Bind(_botConfig);
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
        _pluginManager.MarkBotReady();
        await _pluginManager.RegisterCommandsAsync();
    }

    private async Task HandleInteractionAsync(SocketInteraction interaction)
    {
        var commandName = interaction is SocketSlashCommand slash ? slash.CommandName : "N/A";

        // ── GATE CHECK 1: Early permission validation before any processing ──
        // This runs before deferral so we can reject unauthorized users immediately.
        if (!CheckPermissionGate(interaction))
        {
            _logger.LogWarning("Permission denied for user {User} ({UserId}) on interaction {Name}",
                interaction.User.Username, interaction.User.Id, commandName);

            try
            {
                await interaction.RespondAsync(
                    "Tu n'as pas la permission d'utiliser cette commande.",
                    ephemeral: true);
            }
            catch
            {
                // If we can't respond (e.g. already acknowledged), best effort
            }

            return;
        }

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

        // ── GATE CHECK 2: Second permission check right before execution ──
        // Defense-in-depth: re-verify even after deferral in case state changed.
        if (!CheckPermissionGate(interaction))
        {
            _logger.LogWarning("Permission denied (post-defer) for user {User} on {Name}",
                interaction.User.Username, commandName);

            try
            {
                await interaction.FollowupAsync(
                    "Tu n'as pas la permission d'utiliser cette commande.",
                    ephemeral: true);
            }
            catch { }

            return;
        }

        try
        {
            // Note: AllowedBotRoleAttribute also runs as a Discord.Net precondition inside
            // ExecuteCommandAsync, providing a THIRD layer of permission checking.
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

    /// <summary>
    /// Gate check: resolves the minimum role for the interaction's target command
    /// and verifies the user holds at least that role. Returns false to block execution.
    /// </summary>
    private bool CheckPermissionGate(SocketInteraction interaction)
    {
        if (interaction.User is not SocketGuildUser guildUser)
            return false; // Only guild interactions are allowed

        var minimumRole = ResolveMinimumRoleForInteraction(interaction);

        return PermissionHelper.HasRole(guildUser, minimumRole, _botConfig);
    }

    /// <summary>
    /// Determines the minimum <see cref="BotRole"/> required for a given interaction
    /// by inspecting the precondition attributes on the matched command and its module.
    /// Defaults to <see cref="BotRole.Administrator"/> if no attribute is found (secure by default).
    /// </summary>
    private BotRole ResolveMinimumRoleForInteraction(SocketInteraction interaction)
    {
        // Try to find command-level precondition first, then fall back to module-level
        if (interaction is SocketSlashCommand slashCommand)
        {
            var cmd = _interactions.Modules
                .SelectMany(m => m.SlashCommands)
                .FirstOrDefault(c => c.Name == slashCommand.CommandName);

            if (cmd is not null)
                return ExtractMinimumRole(cmd.Preconditions);
        }
        else if (interaction is SocketUserCommand userCommand)
        {
            var cmd = _interactions.Modules
                .SelectMany(m => m.ContextCommands)
                .FirstOrDefault(c => c.Name == userCommand.CommandName
                                  && c.CommandType == ApplicationCommandType.User);

            if (cmd is not null)
                return ExtractMinimumRole(cmd.Preconditions);
        }
        else if (interaction is SocketMessageComponent component)
        {
            var customId = component.Data.CustomId;
            foreach (var module in _interactions.Modules)
            {
                foreach (var compCmd in module.ComponentCommands)
                {
                    if (MatchesComponentPattern(compCmd.Name, customId))
                        return ExtractMinimumRole(compCmd.Preconditions);
                }
            }
        }
        else if (interaction is SocketModal modal)
        {
            var customId = modal.Data.CustomId;
            foreach (var module in _interactions.Modules)
            {
                foreach (var modalCmd in module.ModalCommands)
                {
                    if (MatchesComponentPattern(modalCmd.Name, customId))
                        return ExtractMinimumRole(modalCmd.Preconditions);
                }
            }
        }

        // Unknown interaction — default to Administrator (secure by default)
        return BotRole.Administrator;
    }

    /// <summary>
    /// Extracts the minimum role from a command's precondition attributes.
    /// Defaults to Administrator if no <see cref="AllowedBotRoleAttribute"/> is found (secure by default).
    /// </summary>
    private static BotRole ExtractMinimumRole(IReadOnlyCollection<PreconditionAttribute> commandPreconditions)
    {
        var attr = commandPreconditions.OfType<AllowedBotRoleAttribute>().FirstOrDefault();
        if (attr is not null)
            return attr.MinimumRole;

        // No attribute found — secure by default: only Administrator
        return BotRole.Administrator;
    }

    /// <summary>
    /// Checks if a component/modal custom ID matches a registered pattern
    /// (patterns use * as wildcard suffix).
    /// </summary>
    private static bool MatchesComponentPattern(string pattern, string customId)
    {
        if (pattern == customId)
            return true;

        // Discord.Net uses * as wildcard in component interaction patterns
        if (pattern.EndsWith('*'))
        {
            var prefix = pattern[..^1];
            return customId.StartsWith(prefix, StringComparison.Ordinal);
        }

        return false;
    }

    private async Task SlashCommandExecutedAsync(SlashCommandInfo command, IInteractionContext context, IResult result)
    {
        if (!result.IsSuccess)
        {
            _logger.LogError("Slash command /{Command} failed: {Error} ({Reason})",
                command.Name, result.Error, result.ErrorReason);

            // If the precondition (AllowedBotRoleAttribute) blocked execution, inform the user
            if (result.Error == InteractionCommandError.UnmetPrecondition)
            {
                try
                {
                    await context.Interaction.FollowupAsync(
                        result.ErrorReason ?? "Tu n'as pas la permission d'utiliser cette commande.",
                        ephemeral: true);
                }
                catch { }
            }
        }
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
