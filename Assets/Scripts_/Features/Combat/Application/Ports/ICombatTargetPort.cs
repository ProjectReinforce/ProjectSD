using Shared.Kernel;

namespace Features.Combat.Application.Ports
{
    public interface ICombatTargetPort
    {
        float GetDefense(EntityId targetId);
        void ApplyDamage(EntityId targetId, float damage);
    }
}
