using Shared.Kernel;
using Shared.Math;

namespace Features.Zone.Domain
{
    public sealed class Zone : Entity
    {
        public Zone(DomainEntityId id, DomainEntityId casterId, Float3 position, ZoneSpec spec) : base(id)
        {
            CasterId = casterId;
            Position = position;
            Spec = spec;
        }

        public DomainEntityId CasterId { get; }
        public Float3 Position { get; }
        public ZoneSpec Spec { get; }
    }
}
