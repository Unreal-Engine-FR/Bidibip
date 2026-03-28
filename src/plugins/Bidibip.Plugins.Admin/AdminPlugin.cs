using Bidibip.Plugin.Sdk;
using Microsoft.Extensions.Logging;

namespace Bidibip.Plugins.Admin;

[BidibipPlugin]
public sealed class AdminPlugin : IBidibipPlugin
{
    public string Name => "Admin";
    public string Description => "Plugin management commands.";

    public Task InitializeAsync(PluginContext context)
    {
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
