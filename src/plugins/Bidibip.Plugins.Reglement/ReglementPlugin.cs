using Bidibip.Plugin.Sdk;
using Microsoft.Extensions.Logging;

namespace Bidibip.Plugins.Reglement;

[BidibipPlugin]
public sealed class ReglementPlugin : IBidibipPlugin
{
    public string Name => "Reglement";
    public string Description => "Posts rules in a configured channel with an approval button to grant the Member role.";

    internal static string DataPath { get; private set; } = "";

    public Task InitializeAsync(PluginContext context)
    {
        DataPath = context.DataPath;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
