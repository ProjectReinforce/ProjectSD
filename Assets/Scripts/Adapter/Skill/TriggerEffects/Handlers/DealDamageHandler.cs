using UnityEngine;
using SwDreams.Domain.Interfaces;
using SwDreams.Shared.Domain.Interfaces;
using SwDreams.Domain.ValueObjects;

namespace SwDreams.Adapter.Skill.TriggerEffects
{
    /// <summary>
    /// 추가 데미지 효과.
    /// primary = 추가 데미지량 (고정값)
    /// secondary = 범위 (0이면 단일 대상, >0이면 AoE)
    ///
    /// 사용 예: 무기 부가효과 "적중 시 추가 10 데미지", 정수 "주변 적 추가 타격".
    /// </summary>
    public class DealDamageHandler : IEffectActionHandler
    {
        public void Execute(EffectParams parameters, TriggerContext context)
        {
            int damage = Mathf.RoundToInt(parameters.primary);
            float radius = parameters.secondary;

            if (damage <= 0) return;

            if (radius > 0f)
            {
                // AoE
                var hits = Physics2D.OverlapCircleAll(context.position, radius);
                foreach (var hit in hits)
                {
                    if (!hit.CompareTag("Enemy")) continue;
                    var damageable = hit.GetComponent<IDamageable>();
                    if (damageable != null && damageable.IsAlive)
                        damageable.TakeDamage(damage);
                }
            }
            else if (context.target != null)
            {
                // 단일 대상
                var damageable = context.target.GetComponent<IDamageable>();
                if (damageable != null && damageable.IsAlive)
                    damageable.TakeDamage(damage);
            }
        }
    }
}
