using System;

namespace Features.Projectile.Domain
{
    public static class HitResolverFactory
    {
        public static IHitResolver Create(HitType type)
        {
            switch (type)
            {
                case HitType.Single: return new SingleHitResolver();
                case HitType.Piercing: return new PiercingHitResolver();
                case HitType.Bounce: return new BounceHitResolver();
                case HitType.Chain: return new ChainHitResolver();
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
        }
    }
}
