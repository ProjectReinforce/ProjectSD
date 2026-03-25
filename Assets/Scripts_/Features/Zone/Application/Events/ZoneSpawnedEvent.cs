using Features.Zone.Domain;
using Shared.Kernel;
using Shared.Math;

namespace Features.Zone.Application.Events
{
    /// <summary>Published when a zone effect is spawned in the world.</summary>
    public readonly struct ZoneSpawnedEvent
    {
        public ZoneSpawnedEvent(
            DomainEntityId zoneId,
            DomainEntityId casterId,
            Float3 position,
            ZoneSpec spec
        )
        {
            ZoneId = zoneId;
            CasterId = casterId;
            Position = position;
            Spec = spec;
        }

        public DomainEntityId ZoneId { get; }
        public DomainEntityId CasterId { get; }
        public Float3 Position { get; }
        public ZoneSpec Spec { get; }
    }
}
