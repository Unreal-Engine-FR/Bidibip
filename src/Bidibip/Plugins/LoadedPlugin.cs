using Bidibip.Plugin.Sdk;
using Discord.Interactions;

namespace Bidibip.Plugins;

/// <summary>
/// Tracks everything about a loaded plugin: its instance, assembly context,
/// event bus, and registered command modules. One instance per loaded DLL.
/// </summary>
internal sealed class LoadedPlugin
{
    /// <summary>Original DLL path (in the plugins/ folder, not the shadow copy).</summary>
    public required string FilePath { get; init; }

    /// <summary>The isolated AssemblyLoadContext. Unloaded on plugin disposal.</summary>
    public required PluginLoadContext LoadContext { get; init; }

    /// <summary>The plugin's main class (implements <see cref="IBidibipPlugin"/>).</summary>
    public required IBidibipPlugin Instance { get; init; }

    /// <summary>
    /// Weak reference to the LoadContext, used to verify the assembly was properly
    /// garbage-collected after unloading. If still alive after several GC cycles,
    /// the plugin has leaked references.
    /// </summary>
    public required WeakReference WeakRef { get; init; }

    /// <summary>The plugin's dedicated event dispatcher.</summary>
    public required PluginEventBus EventBus { get; init; }

    /// <summary>
    /// Discord.Net InteractionService modules registered by this plugin.
    /// Tracked so they can be removed when the plugin is unloaded.
    /// </summary>
    public List<ModuleInfo> RegisteredModules { get; } = [];
}
