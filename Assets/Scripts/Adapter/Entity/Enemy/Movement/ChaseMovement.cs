using UnityEngine;
using SwDreams.Adapter.Manager;

namespace SwDreams.Adapter.Entity
{
    /// <summary>
    /// 적 이동 전략 인터페이스.
    /// Phase 3에서 RunnerMovement, TankMovement 등 추가.
    /// </summary>
    public interface IEnemyMovementStrategy
    {
        void UpdateMovement(Transform enemy, Transform target, float speed);
    }

    /// <summary>
    /// 기본 추적형 이동. 가장 가까운 플레이어를 직선 추적.
    /// </summary>
    public class ChaseMovement : IEnemyMovementStrategy
    {
        public void UpdateMovement(Transform enemy, Transform target, float speed)
        {
            if (target == null) return;

            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentState != GameManager.GameState.Playing)
                return;

            Vector2 direction = (target.position - enemy.position).normalized;
            enemy.position += (Vector3)(direction * speed * Time.deltaTime);
        }
    }
}
