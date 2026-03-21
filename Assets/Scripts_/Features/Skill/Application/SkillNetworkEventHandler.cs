using Features.Skill.Application.Events;
using Features.Skill.Application.Ports;
using Features.Skill.Domain;
using Shared.EventBus;
using Shared.Kernel;

namespace Features.Skill.Application
{
    public sealed class SkillNetworkEventHandler
    {
        private readonly IEventPublisher _publisher;

        public SkillNetworkEventHandler(
            IEventPublisher publisher,
            ISkillNetworkCallbackPort networkCallbacks
        )
        {
            _publisher = publisher;

            networkCallbacks.OnRemoteSkillCasted = HandleRemoteSkillCasted;
        }

        private void HandleRemoteSkillCasted(
            DomainEntityId skillId,
            DomainEntityId casterId,
            float damage,
            float cooldown,
            float range
        )
        {
            var spec = new SkillSpec(damage, cooldown, range);
            _publisher.Publish(new SkillCastedEvent(skillId, casterId, spec));
        }
    }
}
