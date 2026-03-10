namespace Features.Combat.Domain
{
    public static class DamageRule
    {
        public static float Calculate(float baseDamage, float defense, DamageType damageType)
        {
            if (damageType == DamageType.True)
                return baseDamage;

            var reduced = baseDamage - defense;
            return reduced < 0f ? 0f : reduced;
        }
    }
}
