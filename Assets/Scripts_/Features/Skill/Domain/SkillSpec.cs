using System.Collections.Generic;
using Shared.Kernel;

namespace Features.Skill.Domain
{
    public sealed class SkillSpec : ValueObject
    {
        public SkillSpec(float damage, float cooldown, float range, DeliveryType deliveryType)
        {
            Damage = damage;
            Cooldown = cooldown;
            Range = range;
            DeliveryType = deliveryType;
        }

        public float Damage { get; }
        public float Cooldown { get; }
        public float Range { get; }
        public DeliveryType DeliveryType { get; }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Damage;
            yield return Cooldown;
            yield return Range;
            yield return DeliveryType;
        }
    }
}
