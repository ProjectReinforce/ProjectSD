using UnityEngine;

namespace SwDreams.Adapter.Skill
{
    /// <summary>
    /// 유도 투사체. 매직 미사일용.
    /// 가장 가까운 적을 추적하며, 적이 없으면 직선 이동.
    ///
    /// 프리팹: 기존 Projectile 프리팹과 동일 구조.
    /// Projectile 대신 HomingProjectile 컴포넌트 부착.
    /// </summary>
    public class HomingProjectile : Projectile
    {
        private float rotateSpeed = 300f;
        private Transform target;
        private float retargetTimer;
        private const float RETARGET_INTERVAL = 0.2f;

        /// <summary>
        /// ProjectileEffect에서 기본 Initialize 후 호출.
        /// </summary>
        public void SetHoming(float rotateSpeed)
        {
            this.rotateSpeed = rotateSpeed;
            retargetTimer = 0f;
            FindTarget();
        }

        protected override void MoveStep()
        {
            // 타겟 재탐색
            retargetTimer += Time.deltaTime;
            if (retargetTimer >= RETARGET_INTERVAL)
            {
                retargetTimer = 0f;
                if (target == null || !target.gameObject.activeInHierarchy)
                    FindTarget();
            }

            // 타겟이 있으면 방향 선회
            if (target != null && target.gameObject.activeInHierarchy)
            {
                Vector2 toTarget = ((Vector2)target.position - (Vector2)transform.position).normalized;
                float maxAngle = rotateSpeed * Time.deltaTime;
                direction = RotateTowards(direction, toTarget, maxAngle);
            }

            // 이동
            transform.position += (Vector3)(direction * speed * Time.deltaTime);
            UpdateRotation(direction);
        }

        private void FindTarget()
        {
            target = null;
            float minDist = float.MaxValue;

            var enemies = GameObject.FindGameObjectsWithTag("Enemy");
            foreach (var e in enemies)
            {
                if (!e.activeInHierarchy) continue;
                float dist = Vector2.Distance(transform.position, e.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    target = e.transform;
                }
            }
        }

        /// <summary>
        /// from → to 방향으로 최대 maxDegrees만큼 회전.
        /// </summary>
        private Vector2 RotateTowards(Vector2 from, Vector2 to, float maxDegrees)
        {
            float fromAngle = Mathf.Atan2(from.y, from.x) * Mathf.Rad2Deg;
            float toAngle = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;

            float newAngle = Mathf.MoveTowardsAngle(fromAngle, toAngle, maxDegrees);
            float rad = newAngle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }

        public override void OnSpawnFromPool()
        {
            base.OnSpawnFromPool();
            target = null;
            retargetTimer = 0f;
        }
    }
}
