using UnityEngine;
using SwDreams.Domain.Interfaces;
using SwDreams.Shared.Domain.Interfaces;
using SwDreams.Adapter.Manager;
using SwDreams.Shared.Managers;

namespace SwDreams.Adapter.Entity
{
    /// <summary>
    /// 원형 지대 공격. IBossAttackPattern 구현.
    ///
    /// 랜덤 플레이어 위치에 경고 → delay 후 폭발 데미지.
    /// Phase 3에서는 동시에 여러 개 생성.
    ///
    /// MonoBehaviour가 아니므로 코루틴 사용 불가.
    /// → BossPhaseManager가 Update에서 지연 폭발 타이머를 관리.
    /// </summary>
    public class CircleZoneAttack : IBossAttackPattern
    {
        private readonly float cooldown;
        private readonly int damage;
        private readonly float delay;       // 경고 → 폭발 딜레이
        private readonly float radius;
        private readonly int zoneCount;     // 동시 생성 수
        private readonly GameObject effectPrefab;

        public float Cooldown => cooldown;

        public CircleZoneAttack(float cooldown, int damage, float delay,
            float radius, int zoneCount = 1, GameObject effectPrefab = null)
        {
            this.cooldown = cooldown;
            this.damage = damage;
            this.delay = delay;
            this.radius = radius;
            this.zoneCount = zoneCount;
            this.effectPrefab = effectPrefab;
        }

        public bool CanExecute(float timeSinceLastUse)
        {
            return timeSinceLastUse >= cooldown;
        }

        /// <summary>
        /// 원형 지대 설치. 실제 폭발은 delay 후 BossPhaseManager가 처리.
        /// </summary>
        public void Execute(Transform bossTransform, Transform target)
        {
            var players = GameObject.FindGameObjectsWithTag("Player");
            if (players.Length == 0) return;

            for (int i = 0; i < zoneCount; i++)
            {
                // 랜덤 플레이어 위치 선택
                GameObject targetPlayer = players[Random.Range(0, players.Length)];
                var damageable = targetPlayer.GetComponent<IDamageable>();

                // 살아있는 플레이어만
                if (damageable != null && !damageable.IsAlive)
                {
                    // 살아있는 플레이어 찾기
                    bool found = false;
                    foreach (var p in players)
                    {
                        var d = p.GetComponent<IDamageable>();
                        if (d != null && d.IsAlive)
                        {
                            targetPlayer = p;
                            found = true;
                            break;
                        }
                    }
                    if (!found) return;
                }

                Vector2 zonePos = targetPlayer.transform.position;

                // BossPhaseManager에 지연 폭발 요청
                BossPhaseManager.Instance?.RegisterDelayedExplosion(
                    zonePos, delay, damage, radius, effectPrefab);
            }

            Debug.Log($"[CircleZoneAttack] 원형 지대 {zoneCount}개 설치 ({delay}초 후 폭발)");
        }
    }
}