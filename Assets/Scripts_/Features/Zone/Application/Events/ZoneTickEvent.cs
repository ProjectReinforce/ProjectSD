using Shared.Kernel;

namespace Features.Zone.Application.Events
{
    public readonly struct ZoneTickEvent
    {
        public ZoneTickEvent(EntityId zoneId, EntityId targetId)
        {
            ZoneId = zoneId;
            TargetId = targetId;
        }

        public EntityId ZoneId { get; }
        public EntityId TargetId { get; }
    }
}
