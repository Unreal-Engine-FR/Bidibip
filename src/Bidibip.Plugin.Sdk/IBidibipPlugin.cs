namespace Bidibip.Plugin.Sdk;

public interface IBidibipPlugin : IAsyncDisposable
{
    string Name { get; }
    string Description { get; }

    Task InitializeAsync(PluginContext context);
}
