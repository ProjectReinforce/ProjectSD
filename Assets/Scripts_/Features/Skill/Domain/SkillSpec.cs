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

        public override bool Equals(object obj)
        {
            if (obj is not SkillSpec other) return false;
            return Damage == other.Damage
                && Cooldown == other.Cooldown
                && Range == other.Range
                && DeliveryType == other.DeliveryType;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + Damage.GetHashCode();
                hash = hash * 31 + Cooldown.GetHashCode();
                hash = hash * 31 + Range.GetHashCode();
                hash = hash * 31 + DeliveryType.GetHashCode();
                return hash;
            }
        }
    }
}
