using Features.Zone.Application.Ports;
using Features.Zone.Presentation;
using Shared.Math;
using UnityEngine;

namespace Features.Zone.Bootstrap
{
    public sealed class ZoneEffectAdapter : MonoBehaviour, IZoneEffectPort
    {
        [SerializeField]
        private ZoneView _zonePrefab;

        [SerializeField]
        private Transform _spawnRoot;

        [SerializeField]
        private Color _zoneColor = new Color(0.5f, 0.8f, 1f, 0.6f);

        public void SpawnZone(Float3 position, float radius, float duration)
        {
            if (_zonePrefab == null)
            {
                Debug.LogError("[ZoneEffectAdapter] ZoneView prefab is missing.", this);
                return;
            }

            var worldPosition = position.ToVector3();
            var view = _spawnRoot == null
                ? Instantiate(_zonePrefab, worldPosition, Quaternion.identity)
                : Instantiate(_zonePrefab, worldPosition, Quaternion.identity, _spawnRoot);

            view.Initialize(radius, duration);
            view.SetColor(_zoneColor);
            view.name = $"{_zonePrefab.name}_{Time.time}";
        }
    }
}
