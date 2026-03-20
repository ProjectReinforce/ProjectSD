using System;

namespace Shared.EventBus
{
    public interface IEventSubscriber
    {
        void Subscribe<T>(object owner, Action<T> handler);
        void UnsubscribeAll(object owner);
    }
}
