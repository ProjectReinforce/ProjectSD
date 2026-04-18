using UnityEngine;
using Photon.Pun;
using SwDreams.Domain.Interfaces;
using SwDreams.Shared.Domain.Interfaces;
using SwDreams.Domain.ValueObjects;
using SwDreams.Adapter.Manager;
using SwDreams.Shared.Managers;

namespace SwDreams.Adapter.Skill.TriggerEffects
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
            }
        }

        /// <summary>
        /// 프리팹 없을 때 fallback. 방향별 즉시 데미지.
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

                var hits = Physics2D.OverlapCircleAll(pos, 0.5f);
                foreach (var hit in hits)
                {
                    if (!hit.CompareTag("Enemy")) continue;
                    var damageable = hit.GetComponent<IDamageable>();
                    if (damageable != null && damageable.IsAlive)
                    {
                        damageable.TakeDamage(damage);
                        break;
                    }
                }
            }
        }
    }
}