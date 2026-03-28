using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace Bidibip.Plugin.Sdk.Permissions;

/// <summary>
/// Marks a command with the minimum role allowed to execute it.
/// If not present, the command defaults to <see cref="BotRole.Administrator"/> (secure by default).
/// Must be placed on each command method individually.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class AllowedBotRoleAttribute : PreconditionAttribute
{
    public BotRole MinimumRole { get; }

    public AllowedBotRoleAttribute(BotRole minimumRole)
    {
        MinimumRole = minimumRole;
    }

    public override Task<PreconditionResult> CheckRequirementsAsync(
        IInteractionContext context,
        ICommandInfo commandInfo,
        IServiceProvider services)
    {
        var botConfig = services.GetService(typeof(BotConfig)) as BotConfig;
        if (botConfig is null)
            return Task.FromResult(PreconditionResult.FromError("Bot configuration is unavailable."));

        if (context.User is not SocketGuildUser guildUser)
            return Task.FromResult(PreconditionResult.FromError("This command can only be used in a server."));

        if (PermissionHelper.HasRole(guildUser, MinimumRole, botConfig))
            return Task.FromResult(PreconditionResult.FromSuccess());

        return Task.FromResult(PreconditionResult.FromError(
            "Tu n'as pas la permission d'utiliser cette commande."));
    }
}
