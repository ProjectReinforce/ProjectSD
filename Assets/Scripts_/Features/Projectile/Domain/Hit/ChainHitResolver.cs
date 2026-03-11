namespace Features.Projectile.Domain.Hit
{
    public sealed class ChainHitResolver : IHitResolver
    {
        private readonly int _maxChains;

        public ChainHitResolver(int maxChains = 3)
        {
            _maxChains = maxChains;
        }

        public IHitResult Resolve(Projectile projectile)
        {
            if (projectile.HitCount >= _maxChains)
                return new DestroyHitResult();

            return new ChainHitResult();
        }
    }
}
