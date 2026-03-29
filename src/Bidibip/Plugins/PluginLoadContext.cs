// ──────────────────────────────────────────────────────────────────────────────
// PluginLoadContext.cs — Assembly isolation for plugins
//
// Each plugin is loaded in its own AssemblyLoadContext (ALC). This provides:
//
//   1. TYPE ISOLATION: A plugin can bundle its own dependencies (e.g., a JSON
//      library) without conflicting with another plugin's version.
//
//   2. UNLOADABILITY: The ALC is created with isCollectible: true, which allows
//      the runtime to garbage-collect the entire assembly and its types when
//      the plugin is unloaded. This is what makes hot-reload possible.
//
//   3. TYPE UNIFICATION: Certain assemblies (SDK, Discord.Net, Microsoft.Extensions)
//      are NOT loaded in the plugin's ALC — they fall through to the host's
//      default context (returning null from Load()). This ensures that types like
//      IBidibipPlugin and SocketInteractionContext are the SAME type in both
//      the host and plugin, so casting and interface checks work.
//
// Without this shared-assembly list, a plugin's IBidibipPlugin would be a
// DIFFERENT type than the host's, causing "does not implement IBidibipPlugin"
// errors even though the code looks correct.
// ──────────────────────────────────────────────────────────────────────────────

using System.Reflection;
using System.Runtime.Loader;

namespace Bidibip.Plugins;

/// <summary>
/// A collectible <see cref="AssemblyLoadContext"/> that isolates plugin assemblies
/// while sharing SDK and framework types with the host.
/// </summary>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    /// <param name="pluginPath">
    /// Path to the plugin DLL (shadow copy). Used by <see cref="AssemblyDependencyResolver"/>
    /// to locate the plugin's .deps.json and resolve transitive dependencies.
    /// </param>
    public PluginLoadContext(string pluginPath) : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Returning null tells the runtime to load from the default (host) context.
        // This is required for any type that must be shared between host and plugin.
        if (assemblyName.Name is "Bidibip.Plugin.Sdk"
            or "Discord.Net.Core"
            or "Discord.Net.WebSocket"
            or "Discord.Net.Rest"
            or "Discord.Net.Interactions")
            return null;

        // Microsoft.Extensions (ILogger, IConfiguration, etc.) must also be shared
        // so that constructor injection in command modules works correctly.
        if (assemblyName.Name?.StartsWith("Microsoft.Extensions.") == true)
            return null;

        // For all other assemblies, try to resolve from the plugin's directory.
        // If not found, fall back to the default context (returns null).
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        return assemblyPath != null ? LoadFromAssemblyPath(assemblyPath) : null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return libraryPath != null ? LoadUnmanagedDllFromPath(libraryPath) : IntPtr.Zero;
    }
}
