using UnityEngine;
using SwDreams.Features.Enemy.Adapter;
using SwDreams.Features.Skill.Domain.ValueObjects;
using SwDreams.Features.Skill.Adapter.TriggerEffects;

namespace SwDreams.Features.Skill.Adapter.TriggerEffects
{
    /// <summary>
    /// 적에게 슬로우 부여.
    /// primary = 슬로우 배율 (0.5 = 50% 감속)
    /// secondary = 지속시간 (초)
    ///
    /// 사용 예: 뇌전역 진화 (OnHit → ApplySlow), 얼음 정수 등.
    ///
    /// context.source 가 있으면 EnemyMovement.slowStack 에서 source 별 독립 관리되어 중첩 가능.
    /// null/빈 문자열이면 "__legacy__" 단일 슬롯으로 통합 (기존 동작 호환).
    /// </summary>
    public class ApplySlowHandler : IEffectActionHandler
    {
        public void Execute(EffectParams parameters, TriggerContext context)
        {
            float multiplier = parameters.primary;
            float duration = parameters.secondary;

            if (multiplier <= 0f || multiplier >= 1f)
            {
                Debug.LogWarning($"[ApplySlowHandler] 잘못된 슬로우 배율: {multiplier}");
                return;
            }

            if (duration <= 0f)
            {
                Debug.LogWarning("[ApplySlowHandler] 지속시간 0 이하");
                return;
            }

            string source = context.source;

            if (context.target != null)
            {
                // 단일 대상
                ApplyToTarget(context.target, source, multiplier, duration);
            }
            else
            {
                // 대상 없으면 주변 적 전체 (OnExpire 등에서 사용)
                float radius = parameters.tertiary;
                if (radius > 0f)
                {
                    var hits = Physics2D.OverlapCircleAll(context.position, radius);
                    foreach (var hit in hits)
                    {
                        if (!hit.CompareTag("Enemy")) continue;
                        ApplyToTarget(hit.transform, source, multiplier, duration);
                    }
                }
            }
        }

        private void ApplyToTarget(Transform target, string source, float multiplier, float duration)
        {
            var movement = target.GetComponent<EnemyMovement>();
            if (movement != null)
                movement.ApplySlowTemporary(source, multiplier, duration);
        }
    }
}
