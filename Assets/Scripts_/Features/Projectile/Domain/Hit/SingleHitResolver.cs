namespace Features.Projectile.Domain.Hit
{
    public sealed class SingleHitResolver : IHitResolver
    {
        public IHitResult Resolve(Projectile projectile)
        {
            return new DestroyHitResult();
        }
    }
}
