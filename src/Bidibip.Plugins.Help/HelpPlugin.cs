using Bidibip.Plugin.Sdk;
using Microsoft.Extensions.Logging;

namespace Bidibip.Plugins.Help;

[BidibipPlugin]
public sealed class HelpPlugin : IBidibipPlugin
{
    public string Name => "Help";
    public string Description => "Provides a /help command that lists all available commands.";

    public Task InitializeAsync(PluginContext context)
    {
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
