using System.Text;
using System.Threading.Channels;
using Bidibip.Plugin.Sdk;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Serilog.Events;

namespace Bidibip.Services;

public sealed class DiscordLogService
{
    private DiscordSocketClient? _client;
    private BotConfig? _config;
    private readonly Channel<QueuedLog> _queue = Channel.CreateUnbounded<QueuedLog>();
    private readonly string _logsPath;

    public DiscordLogService(string logsPath)
    {
        _logsPath = logsPath;
    }

    public void Initialize(DiscordSocketClient client, IConfiguration configuration)
    {
        _client = client;
        _config = new BotConfig();
        configuration.GetSection("Bot").Bind(_config);
    }

    public void MarkReady()
    {
        _ = Task.Run(ProcessLoopAsync);
    }

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

    private const int MaxMessageLength = 2000;
    private const int BatchDelayMs = 2000;

    private async Task ProcessLoopAsync()
    {
        while (await _queue.Reader.WaitToReadAsync())
        {
            // Wait a short window to collect multiple log entries
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

    private string FormatLogLine(QueuedLog entry)
    {
        var circle = entry.Level switch
        {
            LogEventLevel.Warning => ":yellow_circle:",
            LogEventLevel.Error => ":red_circle:",
            LogEventLevel.Fatal => ":red_circle:",
            _ => ":green_circle:"
        };

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

    private async Task SendBatchAsync(string text, bool hasRolePing)
    {
        try
        {
            var channel = GetLogChannel();
            if (channel is null)
                return;

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

    private static string FormatSourceContext(string? sourceContext)
    {
        if (sourceContext is null)
            return "";

        // Strip common prefix
        const string prefix = "Bidibip.";
        if (sourceContext.StartsWith(prefix, StringComparison.Ordinal))
            sourceContext = sourceContext[prefix.Length..];

        // Replace dots with :: to match module::source format
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

    private sealed class QueuedLog
    {
        public DateTimeOffset Timestamp { get; init; }
        public LogEventLevel Level { get; init; }
        public string? SourceContext { get; init; }
        public required string Message { get; init; }
        public Exception? Exception { get; init; }
    }
}
