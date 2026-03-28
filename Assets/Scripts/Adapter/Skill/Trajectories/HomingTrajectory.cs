using UnityEngine;

namespace SwDreams.Adapter.Skill.Trajectories
{
    /// <summary>
    /// 유도 궤적. 가장 가까운 적을 추적, 없으면 직선.
    /// [Step 4 체인 비행] SetTarget/FindTargetExcluding 추가.
    /// </summary>
    public class HomingTrajectory : ITrajectoryBehavior
    {
        private float rotateSpeed;
        private Transform target;
        private float retargetTimer;
        private const float RETARGET_INTERVAL = 0.2f;

        public bool Penetrates => false;
        public bool OverridesLifetime => false;

        public HomingTrajectory(float rotateSpeed = 300f)
        {
            this.rotateSpeed = rotateSpeed;
        }

        public void Initialize(Projectile projectile)
        {
            retargetTimer = 0f;
            FindTarget(projectile.transform.position);
        }

        public void Reset()
        {
            target = null;
            retargetTimer = 0f;
        }

        /// <summary>
        /// 외부에서 타겟 직접 지정. 체인 비행 시 Projectile이 호출.
        /// </summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            retargetTimer = 0f; // 즉시 추적 시작
        }

        /// <summary>
        /// 제외 목록을 사용하여 가장 가까운 적 탐색.
        /// 체인 비행 시 이미 맞은 적을 제외.
        /// </summary>
        /// <returns>찾은 타겟. 없으면 null.</returns>
        public Transform FindTargetExcluding(Vector2 fromPosition, float searchRadius,
            System.Collections.Generic.HashSet<int> excludeIds)
        {
            Transform closest = null;
            float minDist = float.MaxValue;
            var enemies = GameObject.FindGameObjectsWithTag("Enemy");

            foreach (var e in enemies)
            {
                if (!e.activeInHierarchy) continue;
                if (excludeIds != null && excludeIds.Contains(e.GetInstanceID())) continue;

                float dist = Vector2.Distance(fromPosition, e.transform.position);
                if (dist > searchRadius) continue;
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = e.transform;
                }
            }
            return closest;
        }

        public void UpdateMovement(Projectile projectile, float deltaTime)
        {
            retargetTimer += deltaTime;
            if (retargetTimer >= RETARGET_INTERVAL)
            {
                retargetTimer = 0f;
                if (target == null || !target.gameObject.activeInHierarchy)
                    FindTarget(projectile.transform.position);
            }

            Vector2 dir = projectile.Direction;

            if (target != null && target.gameObject.activeInHierarchy)
            {
                Vector2 toTarget = ((Vector2)target.position - (Vector2)projectile.transform.position).normalized;
                float maxAngle = rotateSpeed * deltaTime;
                dir = RotateTowards(dir, toTarget, maxAngle);
                projectile.Direction = dir;
            }

            projectile.transform.position += (Vector3)(dir * projectile.Speed * deltaTime);
            projectile.SetRotation(dir);
        }

        private void FindTarget(Vector2 fromPosition)
        {
            target = null;
            float minDist = float.MaxValue;
            var enemies = GameObject.FindGameObjectsWithTag("Enemy");
            foreach (var e in enemies)
            {
                if (!e.activeInHierarchy) continue;
                float dist = Vector2.Distance(fromPosition, e.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    target = e.transform;
                }
            }
        }

        private Vector2 RotateTowards(Vector2 from, Vector2 to, float maxDegrees)
        {
            float fromAngle = Mathf.Atan2(from.y, from.x) * Mathf.Rad2Deg;
            float toAngle = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
            float newAngle = Mathf.MoveTowardsAngle(fromAngle, toAngle, maxDegrees);
            float rad = newAngle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }
    }
}