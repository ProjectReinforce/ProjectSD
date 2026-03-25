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

        public Result Execute(DomainEntityId casterId, Float3 position, Float3 direction, float range, float cooldown)
        {
            var spawnPos = position + direction.Normalized * (range * 0.5f);
            var spec = new ZoneSpec(range, cooldown, ZoneAnchorType.World, ZoneHitType.Tick);
            var id = _clock.NewId();

            _zoneEffect.SpawnZone(spawnPos, spec.Radius, spec.Duration);
            _eventBus.Publish(new ZoneSpawnedEvent(id, casterId, spawnPos, spec));
            return Result.Success();
        }
    }
}
