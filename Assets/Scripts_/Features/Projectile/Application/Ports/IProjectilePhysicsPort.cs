using Features.Projectile.Domain;

namespace Features.Projectile.Application.Ports
{
    public interface IProjectilePhysicsPort
    {
        void Spawn(Projectile projectile, ITrajectory trajectory, IHitResolver hitResolver);
    }
}
