using Features.Skill.Application.Ports;
using Features.Skill.Domain;
using Features.Skill.Domain.Delivery;
using Shared.Kernel;
using Shared.Math;

using DomainSkill = Features.Skill.Domain.Skill;

namespace Features.Skill.Application
{
    public sealed class CastSkillUseCase
    {
        private readonly CooldownTracker _cooldownTracker;
        private readonly ISkillNetworkCommandPort _network;

        public CastSkillUseCase(
            CooldownTracker cooldownTracker,
            ISkillNetworkCommandPort network
        )
        {
            _cooldownTracker = cooldownTracker;
            _network = network;
        }

        public Result Execute(
            DomainSkill skill,
            int slotIndex,
            DomainEntityId casterId,
            float currentTime,
            Float3 position,
            Float3 direction
        )
        {
            var cooldownCheck = CooldownRule.CanCast(skill, currentTime, _cooldownTracker);
            if (cooldownCheck.IsFailure)
                return cooldownCheck;

            var result = skill.Delivery.Deliver(skill.Id, casterId, skill.Spec);
            var pr = result as ProjectileDeliveryResult;

            _cooldownTracker.RecordCast(skill.Id, currentTime);

            _network.SendSkillCasted(
                new SkillCastNetworkData(
                    skill.Id,
                    casterId,
                    slotIndex,
                    skill.Spec.Damage,
                    skill.Spec.Cooldown,
                    skill.Spec.Range,
                    result.DeliveryType,
                    pr != null ? (int)pr.ProjectileSpec.TrajectoryType : 0,
                    pr != null ? (int)pr.ProjectileSpec.HitType : 0,
                    pr?.ProjectileSpec.Speed ?? 0f,
                    pr?.ProjectileSpec.Radius ?? 0f,
                    position,
                    direction
                )
            );

            return Result.Success();
        }
    }
}
