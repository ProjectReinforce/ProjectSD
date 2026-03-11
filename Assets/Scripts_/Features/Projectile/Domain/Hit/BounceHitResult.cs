namespace Features.Projectile.Domain.Hit
{
    public sealed class BounceHitResult : IHitResult
    {
        public void Apply(Projectile projectile)
        {
            projectile.RegisterHit();
        }
    }
}
