using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Bidibip.Plugin.Sdk;

public sealed class PluginContext
{
    public required ILogger Logger { get; init; }
    public required IConfiguration Configuration { get; init; }
    public required IEventBus Events { get; init; }
    public required BotConfig BotConfig { get; init; }
    public required ICommandRegistry Commands { get; init; }

    /// <summary>
    /// Directory path where the plugin can persist data files.
    /// </summary>
    public required string DataPath { get; init; }
}
