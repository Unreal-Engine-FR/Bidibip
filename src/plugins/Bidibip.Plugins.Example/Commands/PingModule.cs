using Bidibip.Plugin.Sdk.Permissions;
using Discord.Interactions;
using Discord.WebSocket;

namespace Bidibip.Plugins.Example.Commands;

/// <summary>
/// Example slash command module. Key points:
///   - Inherit from <c>InteractionModuleBase&lt;SocketInteractionContext&gt;</c>
///   - Each command needs [SlashCommand] and [AllowedBotRole] attributes
///   - Use <c>FollowupAsync()</c> because the bot auto-defers most commands
///   - If your command needs a modal, see the "sanction" command exclusion in BotService.cs
/// </summary>
public class PingModule : InteractionModuleBase<SocketInteractionContext>
{
    // [AllowedBotRole(BotRole.Everyone)] means anyone can use this command.
    // If you omit this attribute, it defaults to Administrator (secure by default).
    [SlashCommand("ping", "Replies with pong!")]
    [AllowedBotRole(BotRole.Everyone)]
    public async Task PingAsync()
    {
        // FollowupAsync (not RespondAsync) because BotService auto-defers
        // slash commands. The interaction is already acknowledged by the time
        // this method runs.
        await FollowupAsync("Pong from Example plugin!");
    }
}
