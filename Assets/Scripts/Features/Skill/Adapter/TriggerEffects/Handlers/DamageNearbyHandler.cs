using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using SwDreams.Features.Skill.Domain.ValueObjects;
using SwDreams.Features.Skill.Adapter.TriggerEffects;
using SwDreams.Shared.Domain.Interfaces;

namespace SwDreams.Features.Skill.Adapter.TriggerEffects
{
    /// <summary>
    /// 적중 위치 주변에서 가까운 N마리에게 고정 데미지.
    /// Chain(선형 튕김) 이나 DealDamage(반경 내 전부) 와 달리
    /// "반경 내에서 가까운 순 N마리 선택 후 각자 동일 데미지" 규칙.
    ///
    /// 사용 예: 번개 정수(OnHit → DamageNearby).
    ///
    /// primary   = 탐색 반경
    /// secondary = 최대 대상 수 (N)
    /// tertiary  = 각 대상당 데미지 (고정)
    ///
    /// 원본 타겟(context.target)은 제외 — 이미 스킬 본체 데미지를 받음.
    /// </summary>
    public class DamageNearbyHandler : IEffectActionHandler
    {
        // 재사용 버퍼 — 매 Execute 마다 new List 로 GC 발생 방지.
        private static readonly List<(Transform t, float sqrDist, IDamageable dmg)> _buffer
            = new List<(Transform, float, IDamageable)>(16);

        public void Execute(EffectParams parameters, TriggerContext context)
        {
            if (!PhotonNetwork.IsMasterClient) return;

            float radius = parameters.primary;
            int maxCount = Mathf.RoundToInt(parameters.secondary);
            int damage = Mathf.RoundToInt(parameters.tertiary);

            if (radius <= 0f || maxCount <= 0 || damage <= 0) return;

            _buffer.Clear();

            Vector2 origin = context.position;
            Transform excluded = context.target;

            var hits = Physics2D.OverlapCircleAll(origin, radius);
            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (!hit.CompareTag("Enemy")) continue;
                if (excluded != null && hit.transform == excluded) continue;

                var dmg = hit.GetComponent<IDamageable>();
                if (dmg == null || !dmg.IsAlive) continue;

                float sqrDist = ((Vector2)hit.transform.position - origin).sqrMagnitude;
                _buffer.Add((hit.transform, sqrDist, dmg));
            }

            if (_buffer.Count == 0) return;

            // 가까운 순 정렬
            _buffer.Sort((a, b) => a.sqrDist.CompareTo(b.sqrDist));

            int applyCount = Mathf.Min(maxCount, _buffer.Count);
            for (int i = 0; i < applyCount; i++)
                _buffer[i].dmg.TakeDamage(damage);
        }
    }
}
