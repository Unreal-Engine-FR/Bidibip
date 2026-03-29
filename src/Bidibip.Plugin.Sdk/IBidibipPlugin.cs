namespace Bidibip.Plugin.Sdk;

/// <summary>
/// The core interface that every Bidibip plugin must implement. The host discovers
/// plugin classes by looking for types that implement this interface AND have the
/// <see cref="BidibipPluginAttribute"/>.
///
/// <para><b>Lifecycle:</b></para>
/// <list type="number">
///   <item><description>The host instantiates the plugin via parameterless constructor</description></item>
///   <item><description><see cref="InitializeAsync"/> is called with a <see cref="PluginContext"/> containing the logger, config, event bus, etc.</description></item>
///   <item><description>The plugin subscribes to events and sets up its state</description></item>
///   <item><description>When unloaded (hot-reload or shutdown), <see cref="IAsyncDisposable.DisposeAsync"/> is called for cleanup</description></item>
/// </list>
/// </summary>
/// <example>
/// <code>
/// [BidibipPlugin]
/// public sealed class MyPlugin : IBidibipPlugin
/// {
///     public string Name => "MyPlugin";
///     public string Description => "Does something useful.";
///
///     public Task InitializeAsync(PluginContext context)
///     {
///         context.Events.OnMessageReceived(async msg => { /* ... */ });
///         return Task.CompletedTask;
///     }
///
///     public ValueTask DisposeAsync() => ValueTask.CompletedTask;
/// }
/// </code>
/// </example>
public interface IBidibipPlugin : IAsyncDisposable
{
    /// <summary>Display name shown in /help and the Admin plugin's /plugin list.</summary>
    string Name { get; }

    /// <summary>Short description of what the plugin does.</summary>
    string Description { get; }

    /// <summary>
    /// Called once after the plugin is instantiated. Use this to:
    /// <list type="bullet">
    ///   <item><description>Store references to the logger, config, and client</description></item>
    ///   <item><description>Subscribe to events via <see cref="PluginContext.Events"/></description></item>
    ///   <item><description>Load persistent data via <see cref="PluginData"/></description></item>
    ///   <item><description>Start background tasks</description></item>
    /// </list>
    /// </summary>
    Task InitializeAsync(PluginContext context);
}
