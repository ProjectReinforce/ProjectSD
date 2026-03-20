using Features.Zone.Application.Events;
using Features.Zone.Application.Ports;
using Features.Zone.Domain;
using Shared.EventBus;
using Shared.Kernel;
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

        public Result Execute(DomainEntityId casterId, ZoneSpec spec)
        {
            var zone = new Domain.Zone(_clock.NewId(), casterId, spec);

            _zoneEffect.Spawn(zone);
            _eventBus.Publish(new ZoneSpawnedEvent(zone.Id, casterId));
            return Result.Success();
        }
    }
}
