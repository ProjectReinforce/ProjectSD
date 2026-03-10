namespace Features.Projectile.Domain
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
