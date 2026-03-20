using Shared.Kernel;

namespace Features.Zone.Domain
{
    public sealed class Zone : Entity
    {
        public Zone(DomainEntityId id, DomainEntityId casterId, ZoneSpec spec) : base(id)
        {
            CasterId = casterId;
            Spec = spec;
        }

        public DomainEntityId CasterId { get; }
        public ZoneSpec Spec { get; }
    }
}
