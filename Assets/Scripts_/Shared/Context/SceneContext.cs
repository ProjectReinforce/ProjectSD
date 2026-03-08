using  Shared.EventBus;
using UnityEngine;

namespace Shared.Context
{
    public sealed class SceneContext : MonoBehaviour
    {
        private readonly EventBus.EventBus _eventBus = new EventBus.EventBus();

        public IEventPublisher Publisher => _eventBus;
        public IEventSubscriber Subscriber => _eventBus;
    }
}
