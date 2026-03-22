using System;
using System.Collections.Generic;

namespace Shared.EventBus
{
    public sealed class EventBus : IEventPublisher, IEventSubscriber
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers =
            new Dictionary<Type, List<Delegate>>();
        private readonly Dictionary<object, List<(Type type, Delegate handler)>> _ownerMap =
            new Dictionary<object, List<(Type, Delegate)>>();

        public void Subscribe<T>(object owner, Action<T> handler)
        {
            var type = typeof(T);
            if (!_handlers.TryGetValue(type, out var list))
            {
                list = new List<Delegate>();
                _handlers[type] = list;
            }
            list.Add(handler);

            if (!_ownerMap.TryGetValue(owner, out var ownerList))
            {
                ownerList = new List<(Type, Delegate)>();
                _ownerMap[owner] = ownerList;
            }
            ownerList.Add((type, handler));
        }

        public void UnsubscribeAll(object owner)
        {
            if (!_ownerMap.TryGetValue(owner, out var list))
                return;

            foreach (var (type, handler) in list)
            {
                if (_handlers.TryGetValue(type, out var handlers))
                    handlers.Remove(handler);
            }

            _ownerMap.Remove(owner);
        }

        public void Clear()
        {
            _handlers.Clear();
            _ownerMap.Clear();
        }

        public void Publish<T>(T e)
        {
            if (!_handlers.TryGetValue(typeof(T), out var list))
                return;
            var count = list.Count;
            if (count == 0)
                return;
            var snapshot = new Delegate[count];
            list.CopyTo(snapshot);
            for (var i = 0; i < count; i++)
            {
                try
                {
                    ((Action<T>)snapshot[i])(e);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogException(ex);
                }
            }
        }
    }
}
