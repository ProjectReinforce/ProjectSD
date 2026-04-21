using UnityEngine;

namespace SwDreams.Features.Enemy.Adapter
{
    /// <summary>
    /// 고정형 이동 전략. 위치가 변하지 않는다.
    /// 원거리형(EnemyType.Ranged + RangedBehavior.Stationary)에서 사용.
    /// </summary>
    public class StationaryMovement : IEnemyMovementStrategy
    {
        public void UpdateMovement(Transform enemy, Transform target, float speed)
        {
            // 위치 고정
        }
    }
}
