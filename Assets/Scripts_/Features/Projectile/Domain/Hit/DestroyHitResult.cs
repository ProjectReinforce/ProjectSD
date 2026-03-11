namespace Features.Projectile.Domain.Hit
{
    public sealed class DestroyHitResult : IHitResult
    {
        public void Apply(Projectile projectile)
        {
            projectile.RegisterHit();
            projectile.Destroy();
        }
    }
}
