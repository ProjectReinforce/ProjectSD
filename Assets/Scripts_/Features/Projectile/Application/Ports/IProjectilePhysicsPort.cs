using Features.Projectile.Domain;
using Features.Projectile.Domain.Hit;
using Features.Projectile.Domain.Trajectory;

namespace Features.Projectile.Application.Ports
{
    public interface IProjectilePhysicsPort
    {
        void Spawn(Projectile projectile, ITrajectory trajectory, IHitResolver hitResolver);
    }
}
