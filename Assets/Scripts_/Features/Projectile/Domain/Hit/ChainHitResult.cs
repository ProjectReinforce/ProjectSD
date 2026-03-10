namespace Features.Projectile.Domain
{
    public sealed class ChainHitResult : IHitResult
    {
        public void Apply(Projectile projectile)
        {
            projectile.RegisterHit();
        }
    }
}
