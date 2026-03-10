using Shared.Kernel;

namespace Features.Projectile.Domain
{
    public sealed class Projectile : Entity
    {
        public Projectile(EntityId id, EntityId ownerId, ProjectileSpec spec) : base(id)
        {
            OwnerId = ownerId;
            Spec = spec;
            IsAlive = true;
        }

        public EntityId OwnerId { get; }
        public ProjectileSpec Spec { get; }
        public bool IsAlive { get; private set; }

        public void Destroy()
        {
            IsAlive = false;
        }
    }
}
