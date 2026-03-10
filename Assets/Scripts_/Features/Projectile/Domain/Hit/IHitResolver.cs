namespace Features.Projectile.Domain
{
    public interface IHitResolver
    {
        IHitResult Resolve(Projectile projectile);
    }
}
