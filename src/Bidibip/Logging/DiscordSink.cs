using Bidibip.Services;
using Serilog.Core;
using Serilog.Events;

namespace Bidibip.Logging;

/// <summary>
/// A Serilog sink that forwards log events to <see cref="DiscordLogService"/>.
/// This is the glue between Serilog's pipeline and the Discord log channel.
///
/// Registered in Program.cs via <c>.WriteTo.Sink(new DiscordSink(...))</c>.
/// The sink itself does no filtering or formatting — it just enqueues the raw
/// event. All formatting and batching is handled by <see cref="DiscordLogService"/>.
/// </summary>
public sealed class DiscordSink : ILogEventSink
{
    private readonly DiscordLogService _service;

    public DiscordSink(DiscordLogService service)
    {
        _service = service;
    }

    /// <summary>
    /// Called by Serilog for every log event that passes the minimum level filter.
    /// Must be fast and non-blocking since Serilog calls this synchronously.
    /// </summary>
    public void Emit(LogEvent logEvent)
    {
        _service.Enqueue(logEvent);
    }
}
