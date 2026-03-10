namespace Features.Projectile.Domain
{
    public sealed class BounceHitResult : IHitResult
    {
        public void Apply(Projectile projectile)
        {
            projectile.RegisterHit();
        }
    }
}
