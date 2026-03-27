namespace Bidibip.Plugin.Sdk;

public interface ICommandRegistry
{
    IReadOnlyList<CommandInfo> GetCommands();
}

public sealed class CommandInfo
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public string? PluginName { get; init; }
}
