using Discord;
using Discord.WebSocket;

namespace Bidibip.Plugin.Sdk;

/// <summary>
/// Allows plugins to subscribe to Discord events. Register handlers in
/// <see cref="IBidibipPlugin.InitializeAsync"/>. All handlers are automatically
/// removed when the plugin is unloaded.
///
/// <para><b>Important:</b> Always check <c>msg.Author.IsBot</c> in message handlers
/// to avoid reacting to the bot's own messages (or other bots).</para>
/// </summary>
/// <example>
/// <code>
/// context.Events.OnMessageReceived(async msg =>
/// {
///     if (msg.Author.IsBot) return;
///     if (msg.Content == "!hello")
///         await msg.Channel.SendMessageAsync("Hello!");
/// });
///
/// context.Events.OnBotReady(async () =>
/// {
///     logger.LogInformation("Bot is ready, starting background tasks...");
/// });
/// </code>
/// </example>
public interface IEventBus
{
    /// <summary>Fires when any message is sent in a visible channel.</summary>
    void OnMessageReceived(Func<IMessage, Task> handler);

    /// <summary>Fires when a message is deleted. The message may not be in cache.</summary>
    void OnMessageDeleted(Func<Cacheable<IMessage, ulong>, Cacheable<IMessageChannel, ulong>, Task> handler);

    /// <summary>Fires when a message is edited. <c>before</c> is from cache (may be empty).</summary>
    void OnMessageUpdated(Func<Cacheable<IMessage, ulong>, IMessage, IMessageChannel, Task> handler);

    /// <summary>Fires when a reaction is added to any message.</summary>
    void OnReactionAdded(Func<IReaction, IMessageChannel, Task> handler);

    /// <summary>Fires when a user joins the server.</summary>
    void OnUserJoined(Func<IGuildUser, Task> handler);

    /// <summary>Fires when a user leaves (or is kicked/banned from) the server.</summary>
    void OnUserLeft(Func<IGuild, IUser, Task> handler);

    /// <summary>
    /// Fires when a moderation action is recorded in the audit log (kick, ban, timeout).
    /// Actions performed by the bot itself are filtered out.
    /// </summary>
    void OnAuditLogCreated(Func<AuditLogEntry, Task> handler);

    /// <summary>
    /// Fires for raw Discord interactions (buttons, select menus, modals).
    /// Slash commands are handled separately by Discord.Net's InteractionService.
    /// </summary>
    void OnInteractionCreated(Func<SocketInteraction, Task> handler);

    /// <summary>Fires when a thread is created in any channel.</summary>
    void OnThreadCreated(Func<SocketThreadChannel, Task> handler);

    /// <summary>
    /// Fires once when the bot is connected and ready. Use this to start
    /// background tasks that need the Discord client to be operational.
    /// </summary>
    void OnBotReady(Func<Task> handler);
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
