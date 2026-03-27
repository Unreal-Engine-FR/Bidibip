using Discord.Interactions;
using Discord.WebSocket;

namespace Bidibip.Plugins.Example.Commands;

public class PingModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("ping", "Replies with pong!")]
    public async Task PingAsync()
    {
        await FollowupAsync("Pong from Example plugin!");
    }
}
