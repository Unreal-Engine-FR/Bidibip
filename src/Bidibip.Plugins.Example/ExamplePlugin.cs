using Bidibip.Plugin.Sdk;
using Microsoft.Extensions.Logging;

namespace Bidibip.Plugins.Example;

[BidibipPlugin]
public sealed class ExamplePlugin : IBidibipPlugin
{
    private ILogger? _logger;

    public string Name => "Example";
    public string Description => "A sample plugin demonstrating the plugin system.";

    public Task InitializeAsync(PluginContext context)
    {
        _logger = context.Logger;
        _logger.LogInformation("Example plugin initialized!");

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
