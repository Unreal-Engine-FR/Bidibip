using Bidibip.Plugin.Sdk.Permissions;

namespace Bidibip.Plugin.Sdk;

/// <summary>
/// Provides read-only access to all registered slash commands across all plugins.
/// Injected into plugins via <see cref="PluginContext.Commands"/>.
/// Used by the Help plugin to dynamically build the <c>/help</c> response.
/// </summary>
public interface ICommandRegistry
{
    /// <summary>Returns all currently registered slash commands with their metadata.</summary>
    IReadOnlyList<CommandInfo> GetCommands();
}

/// <summary>
/// Metadata about a registered slash command, used by <see cref="ICommandRegistry"/>.
/// </summary>
public sealed class CommandInfo
{
    /// <summary>The slash command name (e.g., "warn", "help", "ping").</summary>
    public required string Name { get; init; }

    /// <summary>The command description shown in Discord's command picker.</summary>
    public required string Description { get; init; }

    /// <summary>Which plugin registered this command (null if unknown).</summary>
    public string? PluginName { get; init; }

    /// <summary>Minimum role required to use this command.</summary>
    public BotRole MinimumRole { get; init; } = BotRole.Administrator;
}
