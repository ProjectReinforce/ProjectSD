using Shared.EventBus;
using Shared.Ui;
using UnityEngine;

namespace Shared.Context
{
    public sealed class SceneContext : MonoBehaviour
    {
        private readonly EventBus.EventBus _eventBus = new EventBus.EventBus();
        private readonly UiStackCommandBus _uiCommandBus = new UiStackCommandBus();

        public IEventPublisher Publisher => _eventBus;
        public IEventSubscriber Subscriber => _eventBus;
        public IUiCommandPublisher UiCommandPublisher => _uiCommandBus;
        public IUiCommandSubscriber UiCommandSubscriber => _uiCommandBus;
    }
}
