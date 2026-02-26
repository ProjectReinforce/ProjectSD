using UnityEngine;

namespace SwDreams.Adapter.Entity
{
    /// <summary>
    /// 무리형(Swarm) 이동 전략.
    /// 플레이어를 추적하지 않고 랜덤 방향으로 직진.
    /// 일정 시간 후 소멸 (EnemyMovement에서 lifetime 관리).
    /// 
    /// 배치: Scripts/Adapter/Entity/Enemy/Movement/
    /// 
    /// 5-10마리가 같은 위치에서 동시 스폰되지만,
    /// 각각 약간의 방향 편차를 가져 부채꼴로 퍼짐.
    /// </summary>
    public class SwarmMovement : IEnemyMovementStrategy
    {
        private Vector2 moveDirection;
        private bool isInitialized;

        /// <summary>
        /// 랜덤 방향으로 초기화.
        /// baseAngle이 음수면 완전 랜덤, 양수면 기준 각도 ± 편차.
        /// </summary>
        public void SetRandomDirection(float baseAngle = -1f, float spreadDegrees = 30f)
        {
            float angle;
            if (baseAngle < 0f)
            {
                angle = Random.Range(0f, 360f);
            }
            else
            {
                // 그룹 스폰 시 부채꼴로 퍼지도록
                angle = baseAngle + Random.Range(-spreadDegrees, spreadDegrees);
            }

            float rad = angle * Mathf.Deg2Rad;
            moveDirection = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            isInitialized = true;
        }

        public void UpdateMovement(Transform enemy, Transform target, float speed)
        {
            // target(플레이어) 무시. 방향만 따라 직진.
            if (!isInitialized)
            {
                SetRandomDirection();
            }

            enemy.position += (Vector3)(moveDirection * speed * Time.deltaTime);
        }
    }
}