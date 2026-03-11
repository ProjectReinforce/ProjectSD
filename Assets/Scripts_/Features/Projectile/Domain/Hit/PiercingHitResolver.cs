namespace Features.Projectile.Domain.Hit
{
    public sealed class PiercingHitResolver : IHitResolver
    {
        public IHitResult Resolve(Projectile projectile)
        {
            return new ContinueHitResult();
        }
    }
}
