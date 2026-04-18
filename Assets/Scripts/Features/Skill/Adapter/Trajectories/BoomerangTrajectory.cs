using UnityEngine;
using SwDreams.Features.Skill.Adapter.Trajectories;

namespace SwDreams.Features.Skill.Adapter.Trajectories
{
    /// <summary>
    /// 왕복 궤적. 전방 발사 → 감속 → 복귀 → 도착 시 소멸.
    /// 관통. lifetime을 사거리로 사용.
    /// 기존 BoomerangProjectile 로직 포팅.
    /// </summary>
    public class BoomerangTrajectory : ITrajectoryBehavior
    {
        private enum Phase { Outgoing, Returning }
        private Phase phase;

        private Vector2 startPosition;
        private float maxDistance;

        // 진화: 그래비톤 부메랑
        private bool hasPull;
        private float pullRadius;
        private float pullForce;

        public bool Penetrates => true;
        public bool OverridesLifetime => true; // 자체 종료 로직 사용

        public BoomerangTrajectory(bool hasPull = false, float pullRadius = 2f, float pullForce = 2f)
        {
            this.hasPull = hasPull;
            this.pullRadius = pullRadius;
            this.pullForce = pullForce;
        }

        public void Initialize(Projectile projectile)
        {
            phase = Phase.Outgoing;
            startPosition = projectile.transform.position;
            maxDistance = projectile.Lifetime; // lifetime을 사거리로 사용
        }

        public void Reset()
        {
            phase = Phase.Outgoing;
            startPosition = Vector2.zero;
        }

        public void UpdateMovement(Projectile projectile, float deltaTime)
        {
            switch (phase)
            {
                case Phase.Outgoing:
                    UpdateOutgoing(projectile, deltaTime);
                    break;
                case Phase.Returning:
                    UpdateReturning(projectile, deltaTime);
                    break;
            }
        }

        private void UpdateOutgoing(Projectile projectile, float deltaTime)
        {
            float traveled = Vector2.Distance(startPosition, projectile.transform.position);
            float progress = Mathf.Clamp01(traveled / maxDistance);
            float currentSpeed = projectile.Speed * Mathf.Lerp(1f, 0.2f, progress);

            if (currentSpeed < 0.5f || traveled >= maxDistance)
            {
                phase = Phase.Returning;
                return;
            }

            projectile.transform.position += (Vector3)(projectile.Direction * currentSpeed * deltaTime);
            projectile.transform.Rotate(0, 0, 720f * deltaTime);
        }

        private void UpdateReturning(Projectile projectile, float deltaTime)
        {
            Vector2 toStart = startPosition - (Vector2)projectile.transform.position;
            float dist = toStart.magnitude;

            if (dist < 0.5f)
            {
                projectile.ForceReturn();
                return;
            }

            float progress = 1f - Mathf.Clamp01(dist / maxDistance);
            float returnSpd = projectile.Speed * Mathf.Lerp(0.2f, 1f, progress);

            projectile.Direction = toStart.normalized;
            projectile.transform.position += (Vector3)(projectile.Direction * returnSpd * deltaTime);
            projectile.transform.Rotate(0, 0, 720f * deltaTime);

            // 그래비톤: 복귀 경로 흡인
            if (hasPull && Photon.Pun.PhotonNetwork.IsMasterClient)
            {
                var hits = Physics2D.OverlapCircleAll(projectile.transform.position, pullRadius);
                foreach (var hit in hits)
                {
                    if (!hit.CompareTag("Enemy")) continue;
                    float eDist = Vector2.Distance(projectile.transform.position, hit.transform.position);
                    if (eDist < 0.2f) continue;
                    float amount = pullForce * deltaTime;
                    hit.transform.position = Vector2.MoveTowards(
                        hit.transform.position, projectile.transform.position, amount);
                }
            }
        }
    }
}
