namespace Features.Projectile.Domain
{
    public sealed class SingleHitResolver : IHitResolver
    {
        public IHitResult Resolve(Projectile projectile)
        {
            return new DestroyHitResult();
        }
    }
}
