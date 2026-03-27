namespace Bidibip.Plugin.Sdk;

public interface IPluginManager
{
    IReadOnlyList<PluginInfo> GetAllPlugins();
    Task<bool> EnablePluginAsync(string pluginName);
    Task<bool> DisablePluginAsync(string pluginName);
}

public sealed class PluginInfo
{
    public required string FileName { get; init; }
    public required string Name { get; init; }
    public required bool IsLoaded { get; init; }
}
