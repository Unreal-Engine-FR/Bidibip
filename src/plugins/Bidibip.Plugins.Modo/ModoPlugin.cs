using Bidibip.Plugin.Sdk;
using Microsoft.Extensions.Logging;

namespace Bidibip.Plugins.Modo;

[BidibipPlugin]
public sealed class ModoPlugin : IBidibipPlugin
{
    public string Name => "Modo";
    public string Description => "Support ticket system via private threads.";

    internal static string DataPath { get; private set; } = "";

    public async Task InitializeAsync(PluginContext context)
    {
        DataPath = context.DataPath;
        var configPath = Path.Combine(context.DataPath, "config.json");
        await PluginData.LoadAsync<ModoConfig>(configPath);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
