using Features.Skill.Application.Events;
using Features.Skill.Domain;
using Shared.EventBus;
using Shared.Kernel;

namespace Features.Skill.Application.Delivery
{
    public sealed class TargetedDelivery : IDeliveryStrategy
    {
        private readonly IEventPublisher _eventBus;

        public TargetedDelivery(IEventPublisher eventBus)
        {
            _eventBus = eventBus;
        }

        public void Deliver(EntityId skillId, EntityId casterId, SkillSpec spec)
        {
            _eventBus.Publish(new SkillCastedEvent(skillId, casterId, spec));
        }
    }
}
