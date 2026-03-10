namespace Features.Projectile.Domain
{
    public sealed class ContinueHitResult : IHitResult
    {
        public void Apply(Projectile projectile)
        {
            projectile.RegisterHit();
        }
    }
}
