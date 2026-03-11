using Features.Zone.Domain;

namespace Features.Zone.Application.Ports
{
    public interface IZoneEffectPort
    {
        void Spawn(Domain.Zone zone);
    }
}
