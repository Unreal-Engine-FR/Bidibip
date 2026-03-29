// ──────────────────────────────────────────────────────────────────────────────
// BotService.cs — Core Discord connection and interaction handler
//
// This hosted service manages the bot's connection to Discord and handles all
// incoming interactions (slash commands, user context menus, buttons, modals).
//
// Interaction flow:
//   1. Discord sends an interaction via the gateway
//   2. HandleInteractionAsync runs on a background thread (to avoid blocking
//      the gateway thread which would freeze the bot)
//   3. Permission gate #1: check the user's role BEFORE acknowledging
//   4. Auto-defer: acknowledge the interaction so we have 15 minutes to respond
//      (instead of the default 3 seconds). Excluded for commands that need modals.
//   5. Permission gate #2: re-check after defer (defense-in-depth)
//   6. ExecuteCommandAsync: Discord.Net routes to the correct module method,
//      which runs permission gate #3 via the [AllowedBotRole] precondition
//
// The triple-gate approach ensures that even if one check is bypassed due to
// a race condition or framework quirk, the others will catch it.
// ──────────────────────────────────────────────────────────────────────────────

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

        // IMPORTANT: Discord.Net gateway event handlers MUST return immediately.
        // If a handler awaits a long operation, it blocks the gateway thread,
        // which freezes ALL bot activity (no events received, heartbeats missed).
        // That's why we use Task.Run() to offload work to the thread pool.
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
        // This fires after a slash command completes (success or failure).
        // Used to report precondition failures back to the user.
        _interactions.SlashCommandExecuted += SlashCommandExecutedAsync;

        var token = _configuration["Discord:Token"]
            ?? throw new InvalidOperationException("Discord:Token is not configured. Set it in .env via DISCORD__TOKEN.");

        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _client.StopAsync();
    }

    /// <summary>
    /// Called once when the bot has connected and received the READY event from Discord.
    /// This is the earliest point where we can interact with the Discord API
    /// (fetch guilds, register commands, etc.).
    /// </summary>
    private async Task OnReadyAsync()
    {
        _logger.LogInformation("Bot is connected as {User}", _client.CurrentUser);
        // Start forwarding queued log entries to the Discord log channel
        _discordLogService.MarkReady();
        // Notify all loaded plugins that the bot is ready
        await _pluginManager.MarkBotReadyAsync();
        // Sync slash commands with Discord (only if they changed)
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

        // ── Auto-defer ──────────────────────────────────────────────────────
        // Discord gives us 3 seconds to respond to an interaction. After that,
        // the interaction token expires and the user sees "interaction failed".
        // DeferAsync() acknowledges the interaction immediately (shows "Bot is
        // thinking..."), giving us up to 15 minutes to send the real response
        // via FollowupAsync().
        //
        // Exceptions to auto-defer:
        //   - User context menu commands: may need to show a modal as the
        //     initial response (modals can only be the FIRST response)
        //   - "sanction" slash command: also needs a modal for the reason input
        //
        // If a command is deferred, it MUST use FollowupAsync() to respond.
        // If it's NOT deferred, it MUST use RespondAsync() or RespondWithModalAsync().
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
    /// <remarks>
    /// This works for all interaction types:
    /// <list type="bullet">
    ///   <item><description>Slash commands — matched by command name</description></item>
    ///   <item><description>User/message context menus — matched by name + type</description></item>
    ///   <item><description>Button/select/modal interactions — matched by custom ID pattern (supports * wildcard)</description></item>
    /// </list>
    /// </remarks>
    private BotRole ResolveMinimumRoleForInteraction(SocketInteraction interaction)
    {
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

    /// <summary>
    /// Bridges Discord.Net's <see cref="LogMessage"/> to Serilog via <see cref="ILogger"/>.
    /// This lets Discord.Net internal logs (connection events, rate limits, etc.)
    /// appear alongside our own logs in all three sinks (console, file, Discord channel).
    /// </summary>
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
