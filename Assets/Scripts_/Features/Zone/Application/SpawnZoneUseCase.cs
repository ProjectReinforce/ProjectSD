using Features.Zone.Application.Events;
using Features.Zone.Domain;
using Features.Zone.Application.Ports;
using Shared.EventBus;
using Shared.Kernel;
using Shared.Math;
using Shared.Time;

namespace Features.Zone.Application
{
    public sealed class SpawnZoneUseCase
    {
        private readonly IZoneEffectPort _zoneEffect;
        private readonly IClockPort _clock;
        private readonly IEventPublisher _eventBus;

        public SpawnZoneUseCase(IZoneEffectPort zoneEffect, IClockPort clock, IEventPublisher eventBus)
        {
            _zoneEffect = zoneEffect;
            _clock = clock;
            _eventBus = eventBus;
        }

        public Result Execute(DomainEntityId casterId, Float3 position, ZoneSpec spec)
        {
            var zone = new Domain.Zone(_clock.NewId(), casterId, position, spec);

            _zoneEffect.SpawnZone(position, spec.Radius, spec.Duration);
            _eventBus.Publish(new ZoneSpawnedEvent(zone.Id, casterId, position, spec));
            return Result.Success();
        }
    }
}
