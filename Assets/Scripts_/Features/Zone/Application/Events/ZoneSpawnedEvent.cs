using Shared.Kernel;

namespace Features.Zone.Application.Events
{
    public readonly struct ZoneSpawnedEvent
    {
        public ZoneSpawnedEvent(EntityId zoneId, EntityId casterId)
        {
            ZoneId = zoneId;
            CasterId = casterId;
        }

        public EntityId ZoneId { get; }
        public EntityId CasterId { get; }
    }
}
