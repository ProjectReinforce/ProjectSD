namespace Features.Projectile.Domain.Hit
{
    public sealed class ChainHitResult : IHitResult
    {
        public void Apply(Projectile projectile)
        {
            projectile.RegisterHit();
        }
    }
}
