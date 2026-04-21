using UnityEngine;
using Photon.Pun;
using SwDreams.Features.Enemy.Adapter.Data;
using SwDreams.Shared.Managers;

namespace SwDreams.Features.Enemy.Adapter.Attack
{
    /// <summary>
    /// 원거리 적의 공격 사이클 오케스트레이터.
    /// - 타겟 조회는 EnemyTargeter, 쿨다운 관리는 EnemyAttackCooldown 에 위임 (Unity Component 조합)
    /// - 호스트만 Update 로직 수행 (판정 권위)
    /// - RangedAttack.Projectile: SpawnManager 에 투사체 RPC
    /// - RangedAttack.Telegraph:  SpawnManager 에 경고존 RPC
    ///
    /// Ranged 가 아닌 타입에 부착돼도 안전하게 no-op.
    /// Enemy.Initialize 에서 enabled = (type == Ranged) 로 설정.
    /// </summary>
    [RequireComponent(typeof(Enemy))]
    [RequireComponent(typeof(EnemyTargeter))]
    [RequireComponent(typeof(EnemyAttackCooldown))]
    public class EnemyAttack : MonoBehaviour
    {
        private Enemy enemy;
        private EnemyTargeter targeter;
        private EnemyAttackCooldown cooldown;

        private void Awake()
        {
            enemy = GetComponent<Enemy>();
            targeter = GetComponent<EnemyTargeter>();
            cooldown = GetComponent<EnemyAttackCooldown>();
        }

        /// <summary>Enemy.Initialize 직후 호출. 쿨다운 interval 주입 + 초기 딜레이.</summary>
        public void ConfigureFromEnemy()
        {
            if (enemy == null) return;
            cooldown.SetInterval(enemy.AttackInterval);
            cooldown.MarkFiredNow();
        }

        private void Update()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (enemy == null || !enemy.IsAlive) return;
            if (enemy.EnemyType != EnemyType.Ranged) return;

            // 씬 전환 중(GameManager 파괴)엔 안전하게 정지
            if (GameManager.Instance == null) return;
            var state = GameManager.Instance.CurrentState;
            if (state != GameManager.GameState.Playing &&
                state != GameManager.GameState.BossFight)
                return;

            if (!cooldown.CanFire()) return;

            Transform target = targeter.FindClosestAlivePlayer();
            if (target == null) return;

            float range = enemy.AttackRange;
            float sqrDist = ((Vector2)(target.position - transform.position)).sqrMagnitude;
            if (sqrDist > range * range) return;

            FireOnce(target);
            cooldown.MarkFiredNow();
        }

        private void FireOnce(Transform target)
        {
            if (SpawnManager.Instance == null) return;

            Vector2 origin = transform.position;
            Vector2 targetPos = target.position;

            if (enemy.RangedAttackType == RangedAttack.Projectile)
            {
                Vector2 dir = (targetPos - origin);
                if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
                dir = dir.normalized;

                SpawnManager.Instance.RaiseEnemyProjectile(
                    origin, dir, enemy.ProjectileSpeed, enemy.AttackDamage, enemy.ProjectileLifetime);
            }
            else
            {
                // Telegraph: 발사 시점 플레이어 위치를 타겟으로 고정 (예측 샷)
                SpawnManager.Instance.RaiseTelegraph(
                    targetPos, enemy.TelegraphDuration, enemy.TelegraphRadius, enemy.AttackDamage);
            }
        }
    }
}
