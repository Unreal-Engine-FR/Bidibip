using System.Text;
using Bidibip.Plugin.Sdk;
using Bidibip.Plugin.Sdk.Permissions;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace Bidibip.Plugins.Help.Commands;

public class HelpModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ICommandRegistry _commands;

    public HelpModule(ICommandRegistry commands)
    {
        _commands = commands;
    }

    [SlashCommand("help", "Lists all available commands")]
    [AllowedBotRole(BotRole.Everyone)]
    public async Task HelpAsync()
    {
        var commands = _commands.GetCommands();

        var embed = new EmbedBuilder()
            .WithTitle("Available Commands")
            .WithColor(Color.Blue);

        if (commands.Count == 0)
        {
            embed.WithDescription("No commands are currently registered.");
        }
        else
        {
            var grouped = commands
                .GroupBy(c => c.PluginName ?? "Core")
                .OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                var sb = new StringBuilder();
                foreach (var cmd in group.OrderBy(c => c.Name))
                {
                    sb.AppendLine($"`/{cmd.Name}` — {cmd.Description}");
                }

                embed.AddField(group.Key, sb.ToString());
            }
        }

        await FollowupAsync(embed: embed.Build(), ephemeral: true);
    }
}
