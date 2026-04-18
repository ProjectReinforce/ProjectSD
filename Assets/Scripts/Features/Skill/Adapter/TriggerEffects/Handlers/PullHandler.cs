using UnityEngine;
using SwDreams.Features.Skill.Domain.ValueObjects;
using SwDreams.Features.Skill.Adapter.TriggerEffects;
using Photon.Pun;

namespace SwDreams.Features.Skill.Adapter.TriggerEffects
{
    /// <summary>
    /// 주변 적 끌어당김.
    /// primary = 끌어당김 반경
    /// secondary = 끌어당김 힘 (이동 속도)
    ///
    /// 범위 내 적을 context.position 방향으로 즉시 이동.
    /// 매 OnHit/OnInterval에서 호출되어 지속적으로 끌어당기는 효과.
    ///
    /// 사용 예: 그래비톤 부메랑 (OnHit → Pull, 복귀 경로에서)
    /// </summary>
    public class PullHandler : IEffectActionHandler
    {
        public void Execute(EffectParams parameters, TriggerContext context)
        {
            if (!PhotonNetwork.IsMasterClient) return;

            float radius = parameters.primary;
            float force = parameters.secondary;

            if (radius <= 0f || force <= 0f) return;

            var hits = Physics2D.OverlapCircleAll(context.position, radius);
            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Enemy")) continue;

                var rb = hit.GetComponent<Rigidbody2D>();
                if (rb == null) continue;

                // context.position 방향으로 끌어당김
                Vector2 direction = ((Vector2)context.position - rb.position).normalized;
                float distance = Vector2.Distance(context.position, rb.position);

                // 가까울수록 강하게 (선형 감쇄)
                float strength = force * (1f - distance / radius);
                if (strength <= 0f) continue;

                rb.position += direction * strength * Time.deltaTime;
            }
        }
    }
}
