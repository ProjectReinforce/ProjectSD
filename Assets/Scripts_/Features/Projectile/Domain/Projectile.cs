using Shared.Kernel;

namespace Features.Projectile.Domain
{
    public sealed class Projectile : Entity
    {
        public Projectile(DomainEntityId id, DomainEntityId ownerId, ProjectileSpec spec) : base(id)
        {
            OwnerId = ownerId;
            Spec = spec;
            IsAlive = true;
            HitCount = 0;
        }

        public DomainEntityId OwnerId { get; }
        public ProjectileSpec Spec { get; }
        public bool IsAlive { get; private set; }
        public int HitCount { get; private set; }

        public void Destroy()
        {
            IsAlive = false;
        }

        public void RegisterHit()
        {
            HitCount++;
        }
    }
}
