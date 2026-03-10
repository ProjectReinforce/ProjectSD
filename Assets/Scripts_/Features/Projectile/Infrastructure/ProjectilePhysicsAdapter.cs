using Features.Projectile.Application.Ports;
using Features.Projectile.Domain;
using UnityEngine;

namespace Features.Projectile.Infrastructure
{
    public sealed class ProjectilePhysicsAdapter : MonoBehaviour, IProjectilePhysicsPort
    {
        public void Spawn(Domain.Projectile projectile, ITrajectory trajectory, IHitResolver hitResolver)
        {
        }
    }
}
