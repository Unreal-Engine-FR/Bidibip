using Bidibip.Plugin.Sdk;
using Discord;

namespace Bidibip.Plugins;

internal sealed class PluginEventBus : IEventBus
{
    private readonly List<Func<IMessage, Task>> _messageHandlers = [];
    private readonly List<Func<Cacheable<IMessage, ulong>, Cacheable<IMessageChannel, ulong>, Task>> _messageDeletedHandlers = [];
    private readonly List<Func<Cacheable<IMessage, ulong>, IMessage, IMessageChannel, Task>> _messageUpdatedHandlers = [];
    private readonly List<Func<IReaction, IMessageChannel, Task>> _reactionHandlers = [];
    private readonly List<Func<IGuildUser, Task>> _userJoinedHandlers = [];
    private readonly List<Func<IGuild, IUser, Task>> _userLeftHandlers = [];

    public void OnMessageReceived(Func<IMessage, Task> handler) =>
        _messageHandlers.Add(handler);

    public void OnMessageDeleted(Func<Cacheable<IMessage, ulong>, Cacheable<IMessageChannel, ulong>, Task> handler) =>
        _messageDeletedHandlers.Add(handler);

    public void OnMessageUpdated(Func<Cacheable<IMessage, ulong>, IMessage, IMessageChannel, Task> handler) =>
        _messageUpdatedHandlers.Add(handler);

    public void OnReactionAdded(Func<IReaction, IMessageChannel, Task> handler) =>
        _reactionHandlers.Add(handler);

    public void OnUserJoined(Func<IGuildUser, Task> handler) =>
        _userJoinedHandlers.Add(handler);

    public void OnUserLeft(Func<IGuild, IUser, Task> handler) =>
        _userLeftHandlers.Add(handler);

    internal async Task DispatchMessageReceived(IMessage message)
    {
        foreach (var handler in _messageHandlers)
            await handler(message);
    }

    internal async Task DispatchMessageDeleted(Cacheable<IMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel)
    {
        foreach (var handler in _messageDeletedHandlers)
            await handler(message, channel);
    }

    internal async Task DispatchMessageUpdated(Cacheable<IMessage, ulong> before, IMessage after, IMessageChannel channel)
    {
        foreach (var handler in _messageUpdatedHandlers)
            await handler(before, after, channel);
    }

    internal async Task DispatchReactionAdded(IReaction reaction, IMessageChannel channel)
    {
        foreach (var handler in _reactionHandlers)
            await handler(reaction, channel);
    }

    internal async Task DispatchUserJoined(IGuildUser user)
    {
        foreach (var handler in _userJoinedHandlers)
            await handler(user);
    }

    internal async Task DispatchUserLeft(IGuild guild, IUser user)
    {
        foreach (var handler in _userLeftHandlers)
            await handler(guild, user);
    }

    internal void Clear()
    {
        _messageHandlers.Clear();
        _messageDeletedHandlers.Clear();
        _messageUpdatedHandlers.Clear();
        _reactionHandlers.Clear();
        _userJoinedHandlers.Clear();
        _userLeftHandlers.Clear();
    }
}
