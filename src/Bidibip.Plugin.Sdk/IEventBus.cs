using Discord;

namespace Bidibip.Plugin.Sdk;

public interface IEventBus
{
    void OnMessageReceived(Func<IMessage, Task> handler);
    void OnMessageDeleted(Func<Cacheable<IMessage, ulong>, Cacheable<IMessageChannel, ulong>, Task> handler);
    void OnMessageUpdated(Func<Cacheable<IMessage, ulong>, IMessage, IMessageChannel, Task> handler);
    void OnReactionAdded(Func<IReaction, IMessageChannel, Task> handler);
    void OnUserJoined(Func<IGuildUser, Task> handler);
    void OnUserLeft(Func<IGuild, IUser, Task> handler);
}
