using UnityEngine;

namespace SwDreams.Adapter.Entity
{
    /// <summary>
    /// 적의 이동 처리.
    /// 모든 클라이언트에서 로컬 실행 (플레이어 위치가 PhotonTransformView로
    /// 동기화되므로 추적 결과가 거의 동일).
    /// </summary>
    public class EnemyMovement : MonoBehaviour
    {
        private Enemy enemy;
        private IEnemyMovementStrategy movementStrategy;

        public void Initialize(Enemy enemyRef)
        {
            enemy = enemyRef;
            movementStrategy = new ChaseMovement();
        }

        public void SetStrategy(IEnemyMovementStrategy strategy)
        {
            movementStrategy = strategy;
        }

        private void Update()
        {
            if (enemy == null || !enemy.IsAlive) return;

            Transform target = FindClosestPlayer();
            if (target != null && movementStrategy != null)
            {
                movementStrategy.UpdateMovement(transform, target, enemy.MoveSpeed);
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
