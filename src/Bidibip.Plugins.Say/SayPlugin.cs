using Bidibip.Plugin.Sdk;
using Microsoft.Extensions.Logging;

namespace Bidibip.Plugins.Say;

[BidibipPlugin]
public sealed class SayPlugin : IBidibipPlugin
{
    public string Name => "Say";
    public string Description => "Allows administrators to send messages and formatted content through the bot.";

    public Task InitializeAsync(PluginContext context)
    {
        context.Logger.LogInformation("Say plugin initialized");
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
