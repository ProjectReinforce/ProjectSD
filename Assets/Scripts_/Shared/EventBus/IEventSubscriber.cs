using System;

namespace Shared.EventBus
{
    public interface IEventSubscriber
    {
        void Subscribe<T>(Action<T> handler);
        void Subscribe<T>(object owner, Action<T> handler);
        void Unsubscribe<T>(Action<T> handler);
        void UnsubscribeAll(object owner);
    }
}
