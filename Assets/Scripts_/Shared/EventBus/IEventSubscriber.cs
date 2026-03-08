using System;

namespace Shared.EventBus
{
    public interface IEventSubscriber
    {
        void Subscribe<T>(Action<T> handler);
        void Unsubscribe<T>(Action<T> handler);
    }
}
