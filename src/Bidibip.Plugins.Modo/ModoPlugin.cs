using Bidibip.Plugin.Sdk;
using Microsoft.Extensions.Logging;

namespace Bidibip.Plugins.Modo;

[BidibipPlugin]
public sealed class ModoPlugin : IBidibipPlugin
{
    public string Name => "Modo";
    public string Description => "Support ticket system via private threads.";

    internal static string DataPath { get; private set; } = "";

    public Task InitializeAsync(PluginContext context)
    {
        DataPath = context.DataPath;
        context.Logger.LogInformation("Modo plugin initialized.");
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
