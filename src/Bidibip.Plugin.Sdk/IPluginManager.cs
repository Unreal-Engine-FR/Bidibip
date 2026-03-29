namespace Bidibip.Plugin.Sdk;

/// <summary>
/// Allows plugins to manage other plugins at runtime (enable, disable, list).
/// Used by the Admin plugin's <c>/plugin</c> command.
/// </summary>
public interface IPluginManager
{
    /// <summary>Returns all plugins in the plugins folder, whether loaded or disabled.</summary>
    IReadOnlyList<PluginInfo> GetAllPlugins();

    /// <summary>
    /// Enables a previously disabled plugin by name. Removes it from the disabled
    /// list and loads it immediately. Returns false if the plugin was not found or
    /// was already enabled.
    /// </summary>
    Task<bool> EnablePluginAsync(string pluginName);

    /// <summary>
    /// Disables a loaded plugin by name. Unloads it and adds it to the disabled
    /// list (persisted in <c>disabled.json</c>). Returns false if not found.
    /// </summary>
    Task<bool> DisablePluginAsync(string pluginName);
}

/// <summary>
/// Basic info about a plugin in the plugins folder.
/// </summary>
public sealed class PluginInfo
{
    /// <summary>The DLL file name (e.g., "Bidibip.Plugins.Help.dll").</summary>
    public required string FileName { get; init; }

    /// <summary>The plugin's display name (e.g., "Help").</summary>
    public required string Name { get; init; }

    /// <summary>True if the plugin is currently loaded and running.</summary>
    public required bool IsLoaded { get; init; }
}
