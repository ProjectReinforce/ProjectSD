using System;

namespace Features.Projectile.Domain.Hit
{
    public enum HitType
    {
        Single = 0,
        Piercing = 1,
        Bounce = 2,
        Chain = 3
    }

    public interface IHitResult
    {
        void Apply(Projectile projectile);
    }

    public interface IHitResolver
    {
        IHitResult Resolve(Projectile projectile);
    }

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
