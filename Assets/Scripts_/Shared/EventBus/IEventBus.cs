using System;

namespace Shared.EventBus
{
    public interface IEventBus
    {
        void Publish<T>(T e);
        void Subscribe<T>(Action<T> handler);
        void Unsubscribe<T>(Action<T> handler);
    }
}
