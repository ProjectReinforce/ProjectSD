using UnityEngine;

namespace SwDreams.Features.Enemy.Adapter
{
    /// <summary>
    /// 추격형 이동 전략. 타겟과의 거리가 stopDistance 보다 멀면 추적, 이하면 정지.
    /// 원거리형(EnemyType.Ranged + RangedBehavior.Kite)에서 사용.
    ///
    /// Dead Reckoning:
    /// stopDistance가 SO 기반이라 호스트/클라이언트 모두 동일 값으로 동작.
    /// </summary>
    public class KiteMovement : IEnemyMovementStrategy
    {
        private readonly float stopDistance;

        public KiteMovement(float stopDistance)
        {
            this.stopDistance = stopDistance;
        }

        public void UpdateMovement(Transform enemy, Transform target, float speed)
        {
            if (target == null) return;

            float sqrDist = ((Vector2)(target.position - enemy.position)).sqrMagnitude;
            if (sqrDist <= stopDistance * stopDistance) return;

            Vector2 dir = ((Vector2)(target.position - enemy.position)).normalized;
            enemy.position += (Vector3)(dir * speed * Time.deltaTime);
        }
    }
}
