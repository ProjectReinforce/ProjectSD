namespace Features.Projectile.Domain.Hit
{
    public sealed class ContinueHitResult : IHitResult
    {
        public void Apply(Projectile projectile)
        {
            projectile.RegisterHit();
        }
    }
}
