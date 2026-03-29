using Bidibip.Plugin.Sdk;
using Microsoft.Extensions.Logging;

namespace Bidibip.Plugins.Example;

/// <summary>
/// A minimal reference plugin that demonstrates the core plugin patterns:
///   - The [BidibipPlugin] + IBidibipPlugin structure
///   - Subscribing to events via the event bus
///   - A slash command defined in a separate module (see Commands/PingModule.cs)
///
/// Copy this plugin as a starting point for new plugins.
/// See docs/creating-a-plugin.md for a full walkthrough.
/// </summary>
[BidibipPlugin]
public sealed class ExamplePlugin : IBidibipPlugin
{
    private ILogger? _logger;

    public string Name => "Example";
    public string Description => "A sample plugin demonstrating the plugin system.";

    public Task InitializeAsync(PluginContext context)
    {
        _logger = context.Logger;

        // Example: subscribe to message events.
        // Always check IsBot to avoid infinite loops with the bot's own messages.
        context.Events.OnMessageReceived(async msg =>
        {
            if (msg.Author.IsBot) return;

            if (msg.Content.Equals("!hello", StringComparison.OrdinalIgnoreCase))
            {
                await msg.Channel.SendMessageAsync("Hello from the Example plugin!");
            }
        });

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _logger?.LogInformation("Example plugin disposed.");
        return ValueTask.CompletedTask;
    }
}
