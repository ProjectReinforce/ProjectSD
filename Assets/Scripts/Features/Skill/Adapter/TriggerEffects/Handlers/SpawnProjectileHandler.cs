using UnityEngine;
using SwDreams.Features.Skill.Domain.ValueObjects;
using SwDreams.Features.Skill.Adapter.TriggerEffects;
using SwDreams.Features.Skill.Adapter.Data;
using SwDreams.Features.Skill.Adapter;
using Photon.Pun;
using SwDreams.Shared.Domain.Interfaces;
using SwDreams.Shared.Managers;

namespace SwDreams.Features.Skill.Adapter.TriggerEffects
{
    /// <summary>
    /// 추가 투사체 생성.
    /// primary = 개수
    /// secondary = 데미지 배율 (1.0 = context.damage의 100%)
    ///
    /// context.subProjectilePrefab에서 프리팹을 읽음.
    /// SkillData.subProjectilePrefab → ProjectileSpawner → Projectile → TriggerContext로 전달.
    /// 프리팹 미설정 시 방향별 즉시 데미지로 fallback.
    ///
    /// 사용 예: 분기탄 (OnHit → SpawnProjectile)
    /// </summary>
    public class SpawnProjectileHandler : IEffectActionHandler
    {
        public void Execute(EffectParams parameters, TriggerContext context)
        {
            if (!PhotonNetwork.IsMasterClient) return;

            int count = Mathf.RoundToInt(parameters.primary);
            float damageMultiplier = parameters.secondary > 0f ? parameters.secondary : 1f;
            int damage = Mathf.RoundToInt(context.damage * damageMultiplier);

            if (count <= 0) return;

            GameObject prefab = context.subProjectilePrefab;

            if (prefab == null)
            {
                SpawnInstantDamage(count, damage, context);
                return;
            }

            // 균등 방향으로 투사체 생성
            float angleStep = 360f / count;
            float baseAngle = context.direction.sqrMagnitude > 0.01f
                ? Mathf.Atan2(context.direction.y, context.direction.x) * Mathf.Rad2Deg
                : 0f;

            for (int i = 0; i < count; i++)
            {
                float angle = (baseAngle + angleStep * i) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                GameObject obj = PoolManager.Instance.Get(prefab);
                var projectile = obj.GetComponent<Projectile>();

                if (projectile == null)
                {
                    PoolManager.Instance.Return(obj);
                    continue;
                }

                projectile.Initialize(
                    position: context.position,
                    direction: dir,
                    damage: damage,
                    speed: 5f, // TODO: SO에서 읽도록 확장
                    lifetime: 3f,
                    knockbackForce: 0f
                );

                // R9: 자식 투사체에도 부모 critStats 전달 — 자식 적중도 노드별 새 판정.
                projectile.SetCritStats(context.critChance, context.critDamageMultiplier);
            }
        }

        /// <summary>
        /// 프리팹 없을 때 fallback. 방향별 즉시 데미지.
        /// R9: 방향별로 1회 판정 (각 방향 = 새 노드).
        /// </summary>
        private void SpawnInstantDamage(int count, int damage, TriggerContext context)
        {
            float angleStep = 360f / count;
            float searchDist = 1.5f;

            for (int i = 0; i < count; i++)
            {
                float angle = angleStep * i * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 pos = context.position + dir * searchDist;

                int finalDamage = CritJudgment.Roll(damage, context.critChance, context.critDamageMultiplier, out bool isCrit);

                var hits = Physics2D.OverlapCircleAll(pos, 0.5f);
                foreach (var hit in hits)
                {
                    if (!hit.CompareTag("Enemy")) continue;
                    var damageable = hit.GetComponent<IDamageable>();
                    if (damageable != null && damageable.IsAlive)
                    {
                        var enemy = hit.GetComponent<SwDreams.Features.Enemy.Adapter.Enemy>();
                        if (enemy != null)
                        {
                            // B-1a: 가해자 추적 — 사망 시 RPC 페이로드(killerActor/skillId) 진입점.
                            enemy.LastDamagerActorNumber = context.attackerActorNumber;
                            enemy.LastDamagerSkillId = context.sourceSkillId;
                            enemy.TakeDamage(finalDamage, isCrit);
                        }
                        else damageable.TakeDamage(finalDamage);
                        break;
                    }
                }
            }
        }
    }
}