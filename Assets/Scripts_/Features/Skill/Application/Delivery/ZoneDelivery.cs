using Features.Skill.Application.Events;
using Features.Skill.Domain;
using Shared.EventBus;
using Shared.Kernel;

namespace Features.Skill.Application.Delivery
{
    public sealed class ZoneDelivery : IDeliveryStrategy
    {
        private readonly IEventPublisher _eventBus;

        public ZoneDelivery(IEventPublisher eventBus)
        {
            _eventBus = eventBus;
        }

        public void Deliver(EntityId skillId, EntityId casterId, SkillSpec spec)
        {
            _eventBus.Publish(new SkillCastedEvent(skillId, casterId, spec));
        }
    }
}
