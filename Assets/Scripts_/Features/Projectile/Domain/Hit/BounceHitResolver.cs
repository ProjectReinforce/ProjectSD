namespace Features.Projectile.Domain.Hit
{
    public sealed class BounceHitResolver : IHitResolver
    {
        private readonly int _maxBounces;

        public BounceHitResolver(int maxBounces = 3)
        {
            _maxBounces = maxBounces;
        }

        public IHitResult Resolve(Projectile projectile)
        {
            if (projectile.HitCount >= _maxBounces)
                return new DestroyHitResult();

            return new BounceHitResult();
        }
    }
}
