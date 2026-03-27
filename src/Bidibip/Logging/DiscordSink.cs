using Bidibip.Services;
using Serilog.Core;
using Serilog.Events;

namespace Bidibip.Logging;

public sealed class DiscordSink : ILogEventSink
{
    private readonly DiscordLogService _service;

    public DiscordSink(DiscordLogService service)
    {
        _service = service;
    }

    public void Emit(LogEvent logEvent)
    {
        _service.Enqueue(logEvent);
    }
}
