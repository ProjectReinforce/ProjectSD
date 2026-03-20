using Shared.Kernel;

namespace Features.Zone.Application.Events
{
    public readonly struct ZoneSpawnedEvent
    {
        public ZoneSpawnedEvent(DomainEntityId zoneId, DomainEntityId casterId)
        {
            ZoneId = zoneId;
            CasterId = casterId;
        }

        public DomainEntityId ZoneId { get; }
        public DomainEntityId CasterId { get; }
    }
}
