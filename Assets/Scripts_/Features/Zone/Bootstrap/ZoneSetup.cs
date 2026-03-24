using Features.Skill.Application.Events;
using Features.Zone.Application;
using Features.Zone.Domain;
using Shared.EventBus;
using Shared.Math;
using Shared.Time;
using UnityEngine;

namespace Features.Zone.Bootstrap
{
    public sealed class ZoneSetup : MonoBehaviour
    {
        [SerializeField]
        private ZoneEffectAdapter _zoneEffectAdapter;

        private IEventSubscriber _eventBus;
        private SpawnZoneUseCase _spawnZoneUseCase;

        public void Initialize(EventBus eventBus)
        {
            if (_zoneEffectAdapter == null)
            {
                Debug.LogError("[ZoneSetup] ZoneEffectAdapter is not assigned in Inspector.", this);
                return;
            }

            _eventBus = eventBus;
            _spawnZoneUseCase = new SpawnZoneUseCase(
                _zoneEffectAdapter,
                new ClockAdapter(),
                eventBus
            );
            _eventBus.Subscribe(this, new System.Action<ZoneRequestedEvent>(OnZoneRequested));
        }

        private void OnDestroy()
        {
            _eventBus?.UnsubscribeAll(this);
        }

        private void OnZoneRequested(ZoneRequestedEvent e)
        {
            if (_spawnZoneUseCase == null)
            {
                Debug.LogError("[ZoneSetup] SpawnZoneUseCase is not initialized.", this);
                return;
            }

            var result = _spawnZoneUseCase.Execute(
                e.CasterId,
                CalculateSpawnPosition(e.Position, e.Direction, e.Spec.Range),
                new ZoneSpec(e.Spec.Range, e.Spec.Cooldown, ZoneAnchorType.World, ZoneHitType.Tick)
            );

            if (result.IsFailure)
                Debug.LogError($"[ZoneSetup] Spawn failed: {result.Error}", this);
        }

        private static Float3 CalculateSpawnPosition(Float3 position, Float3 direction, float range)
        {
            return position + direction.Normalized * (range * 0.5f);
        }
    }
}
