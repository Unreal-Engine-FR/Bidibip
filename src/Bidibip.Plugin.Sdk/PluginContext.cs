using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Bidibip.Plugin.Sdk;

/// <summary>
/// Everything a plugin needs to operate, provided by the host during
/// <see cref="IBidibipPlugin.InitializeAsync"/>. Store the properties
/// you need as fields in your plugin class.
/// </summary>
public sealed class PluginContext
{
    /// <summary>
    /// Plugin-specific logger. Messages are tagged with "Plugin.{Name}" and
    /// appear in the console, log file, and Discord log channel.
    /// </summary>
    public required ILogger Logger { get; init; }

    /// <summary>
    /// The plugin's own configuration, loaded from
    /// <c>plugins/{PluginDllName}/config.json</c> (not the global config).
    /// Use <see cref="PluginData.LoadAsync{T}"/> for typed access instead.
    /// </summary>
    public required IConfiguration Configuration { get; init; }

    /// <summary>
    /// Subscribe to Discord events (messages, reactions, user joins, etc.).
    /// Handlers registered here are automatically cleaned up on plugin unload.
    /// </summary>
    public required IEventBus Events { get; init; }

    /// <summary>
    /// The global bot configuration: guild ID, role IDs, channel IDs.
    /// Useful for checking role membership or sending to specific channels.
    /// </summary>
    public required BotConfig BotConfig { get; init; }

    /// <summary>
    /// Lists all registered slash commands across all plugins.
    /// Used by the Help plugin to build the /help response.
    /// </summary>
    public required ICommandRegistry Commands { get; init; }

    /// <summary>
    /// Direct access to the Discord WebSocket client. Use for operations
    /// not covered by the event bus (e.g., fetching guilds, downloading users).
    /// </summary>
    public required DiscordSocketClient Client { get; init; }

    /// <summary>
    /// Directory path for persistent storage: <c>Saved/data/{PluginName}/</c>.
    /// The directory is created automatically. Use <see cref="PluginData"/> to
    /// load/save JSON files here.
    /// </summary>
    public required string DataPath { get; init; }
}
