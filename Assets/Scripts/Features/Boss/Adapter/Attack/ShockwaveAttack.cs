using UnityEngine;
using SwDreams.Features.Boss.Domain.Interfaces;
using SwDreams.Features.Boss.Adapter;
using SwDreams.Shared.Domain.Interfaces;

namespace SwDreams.Features.Boss.Adapter
{
    /// <summary>
    /// 부채꼴 충격파 공격. IBossAttackPattern 구현.
    ///
    /// 보스 전방 부채꼴 범위 내 플레이어에게 데미지.
    /// 페이즈별로 쿨다운/데미지가 다르므로, 생성 시 파라미터 주입.
    ///
    /// 비주얼: effectPrefab이 있으면 보스 위치에 생성.
    /// TODO: 이펙트 프리팹은 후속 비주얼 작업에서 추가.
    /// </summary>
    public class ShockwaveAttack : IBossAttackPattern
    {
        private readonly float cooldown;
        private readonly int damage;
        private readonly float halfAngle;  // 반각 (도)
        private readonly float range;
        private readonly GameObject effectPrefab;
        private readonly int fanCount;     // 갈래 수 (도박꾼 보스 효과용)

        public float Cooldown => cooldown;

        public ShockwaveAttack(float cooldown, int damage, float halfAngle,
            float range, GameObject effectPrefab = null, int fanCount = 1)
        {
            this.cooldown = cooldown;
            this.damage = damage;
            this.halfAngle = halfAngle;
            this.range = range;
            this.effectPrefab = effectPrefab;
            this.fanCount = fanCount;
        }

        public bool CanExecute(float timeSinceLastUse)
        {
            return timeSinceLastUse >= cooldown;
        }

        public void Execute(Transform bossTransform, Transform target)
        {
            if (target == null) return;

            Vector2 bossPos = bossTransform.position;
            Vector2 forward = ((Vector2)target.position - bossPos).normalized;

            if (fanCount <= 1)
            {
                // 기본: 단일 부채꼴
                DamagePlayersInFan(bossPos, forward);
            }
            else
            {
                // 도박꾼 보스: 여러 갈래
                float totalSpread = 360f / fanCount;
                for (int i = 0; i < fanCount; i++)
                {
                    float angle = i * totalSpread * Mathf.Deg2Rad;
                    Vector2 dir = new Vector2(
                        forward.x * Mathf.Cos(angle) - forward.y * Mathf.Sin(angle),
                        forward.x * Mathf.Sin(angle) + forward.y * Mathf.Cos(angle)
                    );
                    DamagePlayersInFan(bossPos, dir);
                }
            }

            // TODO: 이펙트 생성 (PoolManager)
            // if (effectPrefab != null) { ... }

            Debug.Log($"[ShockwaveAttack] 충격파 발동 (dmg:{damage}, 갈래:{fanCount})");
        }

        private void DamagePlayersInFan(Vector2 origin, Vector2 forward)
        {
            var players = GameObject.FindGameObjectsWithTag("Player");
            foreach (var player in players)
            {
                var damageable = player.GetComponent<IDamageable>();
                if (damageable == null || !damageable.IsAlive) continue;

                Vector2 toPlayer = (Vector2)player.transform.position - origin;
                float dist = toPlayer.magnitude;

                // 범위 체크
                if (dist > range) continue;

                // 각도 체크
                float angle = Vector2.Angle(forward, toPlayer);
                if (angle > halfAngle) continue;

                damageable.TakeDamage(damage);
            }
        }
    }
}