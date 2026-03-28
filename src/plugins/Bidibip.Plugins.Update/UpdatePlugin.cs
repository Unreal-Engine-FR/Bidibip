using Bidibip.Plugin.Sdk;

namespace Bidibip.Plugins.Update;

[BidibipPlugin]
public sealed class UpdatePlugin : IBidibipPlugin
{
    public string Name => "Update";
    public string Description => "Mise à jour automatique du bot";

    public Task InitializeAsync(PluginContext context) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
