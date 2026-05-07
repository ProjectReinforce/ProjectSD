using UnityEngine;
using SwDreams.Features.Skill.Domain.ValueObjects;
using SwDreams.Features.Skill.Adapter.TriggerEffects;
using SwDreams.Shared.Domain.Interfaces;

namespace SwDreams.Features.Skill.Adapter.TriggerEffects
{
    /// <summary>
    /// 체인 효과. 적중 시 주변 적에게 순차 튕기기.
    /// primary = 체인 횟수
    /// secondary = 탐색 반경
    ///
    /// 사용 예: 체인 미사일 진화 (OnHit → Chain)
    /// </summary>
    public class ChainHandler : IEffectActionHandler
    {
        public void Execute(EffectParams parameters, TriggerContext context)
        {
            int chainCount = Mathf.RoundToInt(parameters.primary);
            float searchRadius = parameters.secondary > 0f ? parameters.secondary : 0.65f;

            if (chainCount <= 0 || context.target == null) return;
            if (!Photon.Pun.PhotonNetwork.IsMasterClient) return;

            Transform currentTarget = context.target;
            int damage = context.damage;

            // 이미 맞은 적 추적
            var hitTargets = new System.Collections.Generic.HashSet<int>();
            hitTargets.Add(currentTarget.GetInstanceID());

            for (int i = 0; i < chainCount; i++)
            {
                Transform next = FindNextTarget(currentTarget.position, searchRadius, hitTargets);
                if (next == null) break;

                var damageable = next.GetComponent<IDamageable>();
                if (damageable != null && damageable.IsAlive)
                {
                    // 체인마다 데미지 감소 (80%씩)
                    int chainDamage = Mathf.RoundToInt(damage * Mathf.Pow(0.8f, i + 1));
                    if (chainDamage < 1) chainDamage = 1;

                    // R9: 노드별 재판정 (§ 9 "체인 / 연쇄폭발 노드별 재판정").
                    int finalDamage = CritJudgment.Roll(chainDamage, context.critChance, context.critDamageMultiplier, out bool isCrit);

                    var enemy = next.GetComponent<SwDreams.Features.Enemy.Adapter.Enemy>();
                    if (enemy != null)
                    {
                        // B-1a: 가해자 추적 — 사망 시 RPC 페이로드(killerActor/skillId) 진입점.
                        enemy.LastDamagerActorNumber = context.attackerActorNumber;
                        enemy.LastDamagerSkillId = context.sourceSkillId;
                        enemy.TakeDamage(finalDamage, isCrit);
                    }
                    else damageable.TakeDamage(finalDamage);
                }

                hitTargets.Add(next.GetInstanceID());
                currentTarget = next;
            }
        }

        private Transform FindNextTarget(Vector2 from, float radius,
            System.Collections.Generic.HashSet<int> exclude)
        {
            var hits = Physics2D.OverlapCircleAll(from, radius);
            Transform closest = null;
            float minDist = float.MaxValue;

            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Enemy")) continue;
                if (exclude.Contains(hit.transform.GetInstanceID())) continue;

                var damageable = hit.GetComponent<IDamageable>();
                if (damageable == null || !damageable.IsAlive) continue;

                float dist = Vector2.Distance(from, hit.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = hit.transform;
                }
            }
            return closest;
        }
    }
}