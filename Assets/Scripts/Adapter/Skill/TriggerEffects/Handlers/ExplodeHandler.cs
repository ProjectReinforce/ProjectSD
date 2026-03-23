using UnityEngine;
using SwDreams.Domain.Interfaces;
using SwDreams.Domain.ValueObjects;

namespace SwDreams.Adapter.Skill.TriggerEffects
{
    /// <summary>
    /// 범위 폭발 효과.
    /// primary = 폭발 반경
    /// secondary = 데미지 배율 (1.0 = context.damage의 100%)
    ///
    /// 사용 예: 폭렬 표창 (OnHit → Explode), 연쇄 폭발 혼돈 등.
    /// </summary>
    public class ExplodeHandler : IEffectActionHandler
    {
        public void Execute(EffectParams parameters, TriggerContext context)
        {
            float radius = parameters.primary;
            float damageMultiplier = parameters.secondary > 0f ? parameters.secondary : 1f;
            int damage = Mathf.RoundToInt(context.damage * damageMultiplier);

            if (radius <= 0f)
            {
                Debug.LogWarning("[ExplodeHandler] 폭발 반경이 0 이하");
                return;
            }

            var hits = Physics2D.OverlapCircleAll(context.position, radius);
            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Enemy")) continue;

                // 트리거 원인이 된 적은 이미 맞았으므로 제외 (선택적)
                if (context.target != null && hit.transform == context.target) continue;

                var damageable = hit.GetComponent<IDamageable>();
                if (damageable != null && damageable.IsAlive)
                    damageable.TakeDamage(damage);
            }

            // TODO: 폭발 이펙트 비주얼 (PoolManager에서 가져오기)
        }
    }
}
