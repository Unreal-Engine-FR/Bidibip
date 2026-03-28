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

    [SlashCommand("help", "Liste des commandes disponibles")]
    [AllowedBotRole(BotRole.Everyone)]
    public async Task HelpAsync()
    {
        var userRole = PermissionHelper.GetHighestRole((SocketGuildUser)Context.User, _botConfig);
        var isOwner = Context.Guild.OwnerId == Context.User.Id;

        var commands = _commands.GetCommands()
            .Where(cmd => isOwner || userRole >= cmd.MinimumRole)
            .ToList();

        var embed = new EmbedBuilder()
            .WithTitle("Aide de Bidibip")
            .WithDescription("Liste des commandes disponibles :")
            .WithColor(Color.DarkGreen);

        foreach (var cmd in commands.OrderBy(c => c.Name))
        {
            var name = cmd.Name.Length > 256 ? cmd.Name[..256] : cmd.Name;
            var description = cmd.Description.Length > 1024 ? cmd.Description[..1024] : cmd.Description;
            embed.AddField(name, description, inline: false);
        }

        await FollowupAsync(embed: embed.Build(), ephemeral: true);
    }
}
