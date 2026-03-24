using Shared.Math;

namespace Features.Zone.Application.Ports
{
    public interface IZoneEffectPort
    {
        void SpawnZone(Float3 position, float radius, float duration);
    }
}
