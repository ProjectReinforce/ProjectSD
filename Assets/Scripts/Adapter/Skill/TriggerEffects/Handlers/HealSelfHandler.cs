using UnityEngine;
using Photon.Pun;
using SwDreams.Domain.ValueObjects;
using SwDreams.Adapter.Entity.Player;

namespace SwDreams.Adapter.Skill.TriggerEffects
{
    /// <summary>
    /// 자기 회복.
    /// primary = 고정 회복량
    /// secondary = 스킬 데미지 비율 회복 (0.1 = context.damage의 10%)
    /// 최종 회복량 = primary + (context.damage × secondary)
    ///
    /// context.owner의 PlayerHealth를 찾아 회복.
    ///
    /// 사용 예:
    /// - 심판의 성역 (OnHit → HealSelf, 적 데미지 비율 회복)
    /// - 적 처치 시 체력 회복 패시브 (OnKill → HealSelf)
    ///
    /// 주의: PlayerHealth.Heal() 메서드 필요.
    /// </summary>
    public class HealSelfHandler : IEffectActionHandler
    {
        public void Execute(EffectParams parameters, TriggerContext context)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (context.owner == null) return;

            float fixedHeal = parameters.primary;
            float ratioHeal = parameters.secondary;

            int healAmount = Mathf.RoundToInt(fixedHeal + context.damage * ratioHeal);
            if (healAmount <= 0) return;

            var health = context.owner.GetComponent<PlayerHealth>();
            if (health == null || !health.IsAlive) return;

            health.Heal(healAmount);
        }
    }
}
