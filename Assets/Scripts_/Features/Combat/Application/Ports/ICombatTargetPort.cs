using Shared.Kernel;

namespace Features.Combat.Application.Ports
{
    public interface ICombatTargetPort
    {
        float GetDefense(DomainEntityId targetId);
        void ApplyDamage(DomainEntityId targetId, float damage);
    }
}
