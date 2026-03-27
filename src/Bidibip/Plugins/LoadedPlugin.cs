using Bidibip.Plugin.Sdk;
using Discord.Interactions;

namespace Bidibip.Plugins;

internal sealed class LoadedPlugin
{
    public required string FilePath { get; init; }
    public required PluginLoadContext LoadContext { get; init; }
    public required IBidibipPlugin Instance { get; init; }
    public required WeakReference WeakRef { get; init; }
    public required PluginEventBus EventBus { get; init; }
    public List<ModuleInfo> RegisteredModules { get; } = [];
}
