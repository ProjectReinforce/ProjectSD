using UnityEngine;
using SwDreams.Data;

namespace SwDreams.Adapter.Entity
{
    /// <summary>
    /// 적의 이동 처리.
    /// 모든 클라이언트에서 로컬 실행 (플레이어 위치가 PhotonTransformView로
    /// 동기화되므로 추적 결과가 거의 동일).
    /// 
    /// Phase 3 변경:
    /// - EnemyType에 따라 이동 전략 자동 선택
    ///   Chaser, Runner, Tank → ChaseMovement (속도만 다름, EnemyData에서 결정)
    ///   Swarm → SwarmMovement (랜덤 방향 직진)
    /// - Swarm 수명 관리 (lifetime 만료 시 ForceReturn)
    /// </summary>
    public class EnemyMovement : MonoBehaviour
    {
        private Enemy enemy;
        private IEnemyMovementStrategy movementStrategy;

        // Swarm 전용: 수명 관리
        private float lifetime;
        private float aliveTimer;
        private bool hasLifetime;

        public void Initialize(Enemy enemyRef)
        {
            enemy = enemyRef;
            hasLifetime = false;
            aliveTimer = 0f;

            // EnemyType에 따라 전략 자동 선택
            movementStrategy = CreateStrategy(enemyRef.EnemyType);
        }

        /// <summary>
        /// Swarm 전용: 이동 방향 + 수명 설정.
        /// SpawnManager에서 Swarm 스폰 시 호출.
        /// </summary>
        public void InitializeSwarm(float baseAngle, float spreadDegrees, float swarmLifetime)
        {
            if (movementStrategy is SwarmMovement swarm)
            {
                swarm.SetRandomDirection(baseAngle, spreadDegrees);
            }

            lifetime = swarmLifetime;
            hasLifetime = true;
            aliveTimer = 0f;
        }

        public void SetStrategy(IEnemyMovementStrategy strategy)
        {
            movementStrategy = strategy;
        }

        private void Update()
        {
            if (enemy == null || !enemy.IsAlive) return;

            if (Manager.GameManager.Instance != null &&
                Manager.GameManager.Instance.CurrentState != Manager.GameManager.GameState.Playing)
                return;

            // Swarm 수명 체크
            if (hasLifetime)
            {
                aliveTimer += Time.deltaTime;
                if (aliveTimer >= lifetime)
                {
                    enemy.ForceReturn();
                    return;
                }
            }

            Transform target = FindClosestPlayer();

            // Swarm은 타겟 없어도 이동해야 함
            if (movementStrategy != null)
            {
                if (target != null || movementStrategy is SwarmMovement)
                {
                    movementStrategy.UpdateMovement(transform, target, enemy.MoveSpeed);
                }
            }
        }

        private IEnemyMovementStrategy CreateStrategy(EnemyType type)
        {
            switch (type)
            {
                case EnemyType.Swarm:
                    return new SwarmMovement();

                case EnemyType.Chaser:
                case EnemyType.Runner:
                case EnemyType.Tank:
                default:
                    return new ChaseMovement();
            }
        }

        private Transform FindClosestPlayer()
        {
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            if (players.Length == 0) return null;

            Transform closest = null;
            float minDist = float.MaxValue;

            foreach (var player in players)
            {
                float dist = Vector2.Distance(transform.position, player.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = player.transform;
                }
            }

            return closest;
        }
    }
}