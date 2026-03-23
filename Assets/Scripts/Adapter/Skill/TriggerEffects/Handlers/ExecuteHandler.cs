using UnityEngine;
using SwDreams.Domain.Interfaces;
using SwDreams.Domain.ValueObjects;

namespace SwDreams.Adapter.Skill.TriggerEffects
{
    /// <summary>
    /// 즉사 효과. HP가 일정 비율 이하인 적을 즉사시킴 (보스 제외).
    /// primary = HP 비율 임계값 (0.15 = 15% 이하)
    /// secondary = 범위 (0이면 단일 대상, >0이면 AoE)
    ///
    /// 사용 예: 나락 진화 (OnHit → Execute)
    /// </summary>
    public class ExecuteHandler : IEffectActionHandler
    {
        public void Execute(EffectParams parameters, TriggerContext context)
        {
            float threshold = parameters.primary;
            float radius = parameters.secondary;

            if (threshold <= 0f) return;
            if (!Photon.Pun.PhotonNetwork.IsMasterClient) return;

            if (radius > 0f)
            {
                // AoE 즉사 체크
                var hits = Physics2D.OverlapCircleAll(context.position, radius);
                foreach (var hit in hits)
                {
                    if (!hit.CompareTag("Enemy")) continue;
                    TryExecute(hit.GetComponent<IDamageable>(), hit.GetComponent<Entity.Boss>(), threshold);
                }
            }
            else if (context.target != null)
            {
                // 단일 대상
                TryExecute(
                    context.target.GetComponent<IDamageable>(),
                    context.target.GetComponent<Entity.Boss>(),
                    threshold);
            }
        }

        private void TryExecute(IDamageable damageable, Entity.Boss boss, float threshold)
        {
            // 보스 제외
            if (boss != null) return;
            if (damageable == null || !damageable.IsAlive) return;

            float hpRatio = (float)damageable.CurrentHP / Mathf.Max(1, damageable.MaxHP);
            if (hpRatio <= threshold)
                damageable.TakeDamage(damageable.CurrentHP);
        }
    }
}
