// ──────────────────────────────────────────────────────────────────────────────
// DiscordLogService.cs — Forwards log entries to a Discord channel
//
// This service acts as a bridge between Serilog and Discord. It:
//   1. Receives log events from the DiscordSink (Serilog custom sink)
//   2. Queues them in a System.Threading.Channel (producer-consumer pattern)
//   3. After the bot is connected (MarkReady), a background loop batches
//      entries every 2 seconds and sends them to the configured log channel
//
// Batching is important because Discord has rate limits on message sending.
// Without batching, a burst of log entries would hit the rate limit and
// cause messages to be delayed or dropped.
//
// Error entries get special treatment:
//   - They are sent as individual messages (not batched) to ensure visibility
//   - They ping the configured Support role to alert the team
//   - They are written to a separate error file for detailed stack traces
// ──────────────────────────────────────────────────────────────────────────────

using System.Text;
using System.Threading.Channels;
using Bidibip.Plugin.Sdk;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Serilog.Events;

namespace Bidibip.Services;

/// <summary>
/// Forwards Serilog log entries to a Discord text channel. Entries are queued
/// before the bot connects and flushed once <see cref="MarkReady"/> is called.
/// </summary>
public sealed class DiscordLogService
{
    private DiscordSocketClient? _client;
    private BotConfig? _config;
    /// <summary>Unbounded channel used as a lock-free producer-consumer queue.</summary>
    private readonly Channel<QueuedLog> _queue = Channel.CreateUnbounded<QueuedLog>();
    private readonly string _logsPath;

    public DiscordLogService(string logsPath)
    {
        _logsPath = logsPath;
    }

    /// <summary>
    /// Provides the Discord client and config. Called during <see cref="BotService.StartAsync"/>
    /// before the bot connects, so the service knows where to send log messages.
    /// </summary>
    public void Initialize(DiscordSocketClient client, IConfiguration configuration)
    {
        _client = client;
        _config = new BotConfig();
        configuration.GetSection("Bot").Bind(_config);
    }

    /// <summary>
    /// Starts the background processing loop. Must be called after the Discord
    /// client is connected (READY event), otherwise channel lookups would fail.
    /// </summary>
    public void MarkReady()
    {
        _ = Task.Run(ProcessLoopAsync);
    }

    /// <summary>
    /// Called by <see cref="Bidibip.Logging.DiscordSink"/> for each Serilog event.
    /// This is intentionally synchronous and non-blocking (TryWrite on an unbounded
    /// channel never blocks) so it's safe to call from any thread.
    /// </summary>
    public void Enqueue(LogEvent logEvent)
    {
        string? sourceContext = null;
        if (logEvent.Properties.TryGetValue("SourceContext", out var ctx))
            sourceContext = ctx.ToString().Trim('"');

        _queue.Writer.TryWrite(new QueuedLog
        {
            Timestamp = logEvent.Timestamp,
            Level = logEvent.Level,
            SourceContext = sourceContext,
            Message = logEvent.RenderMessage(),
            Exception = logEvent.Exception
        });
    }

    /// <summary>Discord message length limit. Lines exceeding this are truncated.</summary>
    private const int MaxMessageLength = 2000;
    /// <summary>
    /// How long to wait after the first queued entry before sending a batch.
    /// This groups rapid-fire log entries into a single Discord message.
    /// </summary>
    private const int BatchDelayMs = 2000;

    /// <summary>
    /// Background loop that drains the queue and sends batched messages to Discord.
    /// Runs for the lifetime of the bot.
    /// </summary>
    private async Task ProcessLoopAsync()
    {
        while (await _queue.Reader.WaitToReadAsync())
        {
            // Collect entries that arrive within the batch window
            await Task.Delay(BatchDelayMs);

            var batch = new StringBuilder();
            var hasRolePing = false;

            while (_queue.Reader.TryRead(out var entry))
            {
                if (entry.Level >= LogEventLevel.Error)
                    await WriteErrorFileAsync(entry);

                var line = FormatLogLine(entry);

                // Errors with role pings are sent immediately as their own message
                if (entry.Level >= LogEventLevel.Error)
                {
                    // Flush any pending batch first
                    if (batch.Length > 0)
                    {
                        await SendBatchAsync(batch.ToString(), hasRolePing: false);
                        batch.Clear();
                        hasRolePing = false;
                    }
                    await SendBatchAsync(line, hasRolePing: true);
                    continue;
                }

                // If adding this line would exceed the limit, flush first
                var newLength = batch.Length == 0 ? line.Length : batch.Length + 1 + line.Length;
                if (newLength > MaxMessageLength)
                {
                    await SendBatchAsync(batch.ToString(), hasRolePing);
                    batch.Clear();
                    hasRolePing = false;
                }

                if (batch.Length > 0)
                    batch.Append('\n');
                batch.Append(line);
            }

            // Flush remaining
            if (batch.Length > 0)
                await SendBatchAsync(batch.ToString(), hasRolePing);
        }
    }

    /// <summary>
    /// Formats a single log entry into a Discord message line.
    /// Format: <c>:color_circle: Source::Context @Support message : exception</c>
    /// </summary>
    private string FormatLogLine(QueuedLog entry)
    {
        // Color-coded circles for quick visual scanning in Discord
        var circle = entry.Level switch
        {
            LogEventLevel.Warning => ":yellow_circle:",
            LogEventLevel.Error => ":red_circle:",
            LogEventLevel.Fatal => ":red_circle:",
            _ => ":green_circle:"
        };

        // e.g. "Plugin.FreeForTheMonth" -> "Plugin::FreeForTheMonth"
        var source = FormatSourceContext(entry.SourceContext);

        var sb = new StringBuilder();
        sb.Append(circle);
        sb.Append(' ');
        sb.Append(source);

        if (entry.Level >= LogEventLevel.Error && _config?.Roles.Support is > 0)
        {
            sb.Append($"<@&{_config.Roles.Support}>");
            sb.Append(' ');
        }

        sb.Append(entry.Message);

        if (entry.Exception is not null)
            sb.Append($" : {entry.Exception.Message}");

        var line = sb.ToString();
        // Truncate individual lines that are too long
        if (line.Length > MaxMessageLength)
            line = line[..MaxMessageLength];
        return line;
    }

    /// <summary>
    /// Sends a batch of log lines to the Discord log channel.
    /// When <paramref name="hasRolePing"/> is true, the Support role mention is
    /// allowed so Discord actually pings the team (otherwise mentions are suppressed).
    /// </summary>
    private async Task SendBatchAsync(string text, bool hasRolePing)
    {
        try
        {
            var channel = GetLogChannel();
            if (channel is null)
                return;

            // AllowedMentions controls which @mentions Discord actually processes.
            // We only allow role pings for error messages to avoid unnecessary noise.
            var mentions = hasRolePing
                ? new AllowedMentions { RoleIds = GetSupportRoleList() }
                : AllowedMentions.None;

            await channel.SendMessageAsync(text, allowedMentions: mentions);
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync(
                $"[DiscordLogService] Failed to send log batch: {ex.Message}");
        }
    }

    private List<ulong> GetSupportRoleList()
    {
        if (_config?.Roles.Support is > 0)
            return [_config.Roles.Support];
        return [];
    }

    /// <summary>
    /// Writes a detailed error report to a separate file in the logs directory.
    /// These files contain the full stack trace and inner exceptions, which are
    /// too long for Discord messages. Useful for post-mortem debugging.
    /// </summary>
    private async Task WriteErrorFileAsync(QueuedLog entry)
    {
        var fileName = $"error-{entry.Timestamp.LocalDateTime:yyyyMMdd-HHmmss-fff}.log";
        var filePath = Path.Combine(_logsPath, fileName);

        var sb = new StringBuilder();
        sb.AppendLine($"Error Report - {entry.Timestamp.LocalDateTime:yyyy-MM-dd HH:mm:ss.fff}");
        sb.AppendLine(new string('-', 60));
        sb.AppendLine($"Level: {entry.Level}");
        sb.AppendLine($"Source: {entry.SourceContext ?? "N/A"}");
        sb.AppendLine($"Message: {entry.Message}");

        if (entry.Exception is not null)
        {
            sb.AppendLine();
            sb.AppendLine($"Exception Type: {entry.Exception.GetType().FullName}");
            sb.AppendLine($"Exception Message: {entry.Exception.Message}");
            sb.AppendLine();
            sb.AppendLine("Stack Trace:");
            sb.AppendLine(entry.Exception.StackTrace);

            var inner = entry.Exception.InnerException;
            while (inner is not null)
            {
                sb.AppendLine();
                sb.AppendLine($"--- Inner Exception: {inner.GetType().FullName} ---");
                sb.AppendLine($"Message: {inner.Message}");
                sb.AppendLine("Stack Trace:");
                sb.AppendLine(inner.StackTrace);
                inner = inner.InnerException;
            }
        }

        await File.WriteAllTextAsync(filePath, sb.ToString());
    }

    /// <summary>
    /// Formats the Serilog SourceContext (typically the fully-qualified logger name)
    /// into a compact display format for Discord.
    /// Example: "Bidibip.Plugins.FreeForTheMonth" -> "Plugins::FreeForTheMonth "
    /// </summary>
    private static string FormatSourceContext(string? sourceContext)
    {
        if (sourceContext is null)
            return "";

        const string prefix = "Bidibip.";
        if (sourceContext.StartsWith(prefix, StringComparison.Ordinal))
            sourceContext = sourceContext[prefix.Length..];

        return sourceContext.Replace(".", "::") + " ";
    }

    private ITextChannel? GetLogChannel()
    {
        if (_client?.ConnectionState != ConnectionState.Connected)
            return null;
        if (_config?.Channels.LogChannel is not > 0)
            return null;
        return _client.GetChannel(_config.Channels.LogChannel) as ITextChannel;
    }

    /// <summary>
    /// Internal representation of a log entry in the queue.
    /// Captures just the fields we need, decoupled from Serilog's <see cref="Serilog.Events.LogEvent"/>.
    /// </summary>
    private sealed class QueuedLog
    {
        public DateTimeOffset Timestamp { get; init; }
        public LogEventLevel Level { get; init; }
        /// <summary>The logger name, e.g. "Bidibip.Plugin.FreeForTheMonth".</summary>
        public string? SourceContext { get; init; }
        public required string Message { get; init; }
        public Exception? Exception { get; init; }
    }
}
