using Bidibip.Plugin.Sdk;
using Microsoft.Extensions.Logging;

namespace Bidibip.Plugins.Warn;

[BidibipPlugin]
public sealed class WarnPlugin : IBidibipPlugin
{
    public string Name => "Warn";
    public string Description => "Sanctions & historique des remarques";

    internal static string DataPath { get; private set; } = "";

    public Task InitializeAsync(PluginContext context)
    {
        DataPath = context.DataPath;
        context.Logger.LogInformation("Warn plugin initialized.");
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
