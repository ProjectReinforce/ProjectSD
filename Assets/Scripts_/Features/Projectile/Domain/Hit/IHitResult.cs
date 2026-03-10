namespace Features.Projectile.Domain
{
    public interface IHitResult
    {
        void Apply(Projectile projectile);
    }
}
