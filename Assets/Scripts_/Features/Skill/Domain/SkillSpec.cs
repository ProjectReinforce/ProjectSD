namespace Features.Skill.Domain
{
    public sealed class SkillSpec : Shared.Kernel.ValueObject
    {
        private const int Precision = 4;

        public SkillSpec(float damage, float cooldown, float range)
        {
            Damage = (float)System.Math.Round(damage, Precision);
            Cooldown = (float)System.Math.Round(cooldown, Precision);
            Range = (float)System.Math.Round(range, Precision);
        }

        public float Damage { get; }
        public float Cooldown { get; }
        public float Range { get; }

        public override bool Equals(object obj)
        {
            if (obj is not SkillSpec other) return false;
            return Damage == other.Damage
                && Cooldown == other.Cooldown
                && Range == other.Range;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + Damage.GetHashCode();
                hash = hash * 31 + Cooldown.GetHashCode();
                hash = hash * 31 + Range.GetHashCode();
                return hash;
            }
        }
    }
}
