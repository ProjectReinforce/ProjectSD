using Shared.Kernel;

namespace Features.Zone.Application.Events
{
    public readonly struct ZoneTickEvent
    {
        public ZoneTickEvent(DomainEntityId zoneId, DomainEntityId targetId)
        {
            ZoneId = zoneId;
            TargetId = targetId;
        }

        public DomainEntityId ZoneId { get; }
        public DomainEntityId TargetId { get; }
    }
}
