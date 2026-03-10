using Shared.Kernel;

namespace Features.Zone.Domain
{
    public sealed class Zone : Entity
    {
        public Zone(EntityId id, EntityId casterId, ZoneSpec spec) : base(id)
        {
            CasterId = casterId;
            Spec = spec;
        }

        public EntityId CasterId { get; }
        public ZoneSpec Spec { get; }
    }
}
