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
    private readonly BotConfig _botConfig;

    public HelpModule(ICommandRegistry commands, BotConfig botConfig)
    {
        _commands = commands;
        _botConfig = botConfig;
    }

    [SlashCommand("help", "Lists all available commands")]
    [AllowedBotRole(BotRole.Everyone)]
    public async Task HelpAsync()
    {
        var userRole = PermissionHelper.GetHighestRole((SocketGuildUser)Context.User, _botConfig);
        var isOwner = Context.Guild.OwnerId == Context.User.Id;

        var commands = _commands.GetCommands()
            .Where(cmd => isOwner || userRole >= cmd.MinimumRole)
            .ToList();

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
