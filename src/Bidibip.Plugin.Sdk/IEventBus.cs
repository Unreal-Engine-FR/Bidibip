using Discord;
using Discord.WebSocket;

namespace Bidibip.Plugin.Sdk;

public interface IEventBus
{
    void OnMessageReceived(Func<IMessage, Task> handler);
    void OnMessageDeleted(Func<Cacheable<IMessage, ulong>, Cacheable<IMessageChannel, ulong>, Task> handler);
    void OnMessageUpdated(Func<Cacheable<IMessage, ulong>, IMessage, IMessageChannel, Task> handler);
    void OnReactionAdded(Func<IReaction, IMessageChannel, Task> handler);
    void OnUserJoined(Func<IGuildUser, Task> handler);
    void OnUserLeft(Func<IGuild, IUser, Task> handler);
    void OnAuditLogCreated(Func<AuditLogEntry, Task> handler);
    void OnInteractionCreated(Func<SocketInteraction, Task> handler);
    void OnThreadCreated(Func<SocketThreadChannel, Task> handler);
}

/// <summary>
/// Represents a guild audit log entry dispatched to plugins.
/// </summary>
public sealed class AuditLogEntry
{
    public required ulong UserId { get; init; }
    public required ulong? TargetId { get; init; }
    public required AuditLogActionType ActionType { get; init; }
    public string? Reason { get; init; }
    public required IGuild Guild { get; init; }
    public IUser? TargetUser { get; init; }

    /// <summary>For MemberUpdate actions: the timeout end timestamp (if a timeout was applied).</summary>
    public DateTimeOffset? TimeoutUntil { get; init; }
}

public enum AuditLogActionType
{
    Kick,
    Ban,
    MemberUpdate,
    Other
}
