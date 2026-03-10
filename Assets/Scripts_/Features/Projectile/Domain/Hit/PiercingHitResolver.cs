namespace Features.Projectile.Domain
{
    public sealed class PiercingHitResolver : IHitResolver
    {
        public IHitResult Resolve(Projectile projectile)
        {
            return new ContinueHitResult();
        }
    }
}
