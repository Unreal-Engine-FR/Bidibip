using System.Reflection;
using System.Runtime.Loader;

namespace Bidibip.Plugins;

internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath) : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // SDK and Discord.Net assemblies must be shared with the host
        // so types unify across the plugin boundary
        if (assemblyName.Name is "Bidibip.Plugin.Sdk"
            or "Discord.Net.Core"
            or "Discord.Net.WebSocket"
            or "Discord.Net.Rest"
            or "Discord.Net.Interactions")
            return null;

        // Microsoft.Extensions abstractions must also be shared
        if (assemblyName.Name?.StartsWith("Microsoft.Extensions.") == true)
            return null;

        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        return assemblyPath != null ? LoadFromAssemblyPath(assemblyPath) : null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return libraryPath != null ? LoadUnmanagedDllFromPath(libraryPath) : IntPtr.Zero;
    }
}
