using Shared.Kernel;

namespace Features.Zone.Application.Events
{
    /// <summary>Published each tick when a zone hits a target inside its area.</summary>
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
