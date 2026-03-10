using UnityEngine;

namespace SwDreams.Adapter.Skill
{
    /// <summary>
    /// 왕복 투사체. 부메랑용.
    /// 전방 발사 → 감속 → 발사 위치로 복귀(가속) → 도착 시 소멸.
    /// 갈 때/올 때 모두 관통 데미지.
    /// projectileLifetime을 사거리(유닛)로 사용.
    /// </summary>
    public class BoomerangProjectile : Projectile
    {
        private enum Phase { Outgoing, Returning }
        private Phase phase;

        private Vector2 startPosition;
        private float maxDistance;

        // [Phase 5 진화: 그래비톤 부메랑] 복귀 경로 끌어당김
        private bool hasPullOnReturn;
        private float boomerangPullRadius = 2f;
        private float boomerangPullForce = 2f;

        public void SetBoomerang(Transform player)
        {
            phase = Phase.Outgoing;
            startPosition = transform.position;
            maxDistance = lifetime;
        }

        /// <summary>
        /// 진화: 그래비톤 부메랑. ProjectileEffect에서 호출.
        /// </summary>
        public void SetPullOnReturn(float pullRadius, float pullForce)
        {
            hasPullOnReturn = true;
            boomerangPullRadius = pullRadius;
            boomerangPullForce = pullForce;
        }

        protected override void Update()
        {
            if (Manager.GameManager.Instance != null &&
                Manager.GameManager.Instance.CurrentState != Manager.GameManager.GameState.Playing)
                return;

            MoveStep();

            // 안전장치: 20초 후 강제 회수
            aliveTime += Time.deltaTime;
            if (aliveTime >= 20f)
                ReturnToPool();
        }

        protected override void MoveStep()
        {
            switch (phase)
            {
                case Phase.Outgoing:
                    MoveOutgoing();
                    break;
                case Phase.Returning:
                    MoveReturning();
                    break;
            }
        }

        private void MoveOutgoing()
        {
            float traveled = Vector2.Distance(startPosition, transform.position);
            float progress = Mathf.Clamp01(traveled / maxDistance);

            // 감속: speed → speed*0.2
            float currentSpeed = speed * Mathf.Lerp(1f, 0.2f, progress);

            if (currentSpeed < 0.5f || traveled >= maxDistance)
            {
                phase = Phase.Returning;
                return;
            }

            transform.position += (Vector3)(direction * currentSpeed * Time.deltaTime);
            transform.Rotate(0, 0, 720f * Time.deltaTime);
        }

        private void MoveReturning()
        {
            Vector2 toStart = startPosition - (Vector2)transform.position;
            float dist = toStart.magnitude;

            if (dist < 0.5f)
            {
                ReturnToPool();
                return;
            }

            float progress = 1f - Mathf.Clamp01(dist / maxDistance);
            float returnSpd = speed * Mathf.Lerp(0.2f, 1f, progress);

            direction = toStart.normalized;
            transform.position += (Vector3)(direction * returnSpd * Time.deltaTime);
            transform.Rotate(0, 0, 720f * Time.deltaTime);

            // [진화: 그래비톤] 복귀 경로에서 적 끌어당김
            if (hasPullOnReturn && Photon.Pun.PhotonNetwork.IsMasterClient)
            {
                var hits = Physics2D.OverlapCircleAll(transform.position, boomerangPullRadius);
                foreach (var hit in hits)
                {
                    if (!hit.CompareTag("Enemy")) continue;
                    float eDist = Vector2.Distance(transform.position, hit.transform.position);
                    if (eDist < 0.2f) continue;

                    float amount = boomerangPullForce * Time.deltaTime;
                    hit.transform.position = Vector2.MoveTowards(
                        hit.transform.position, transform.position, amount);
                }
            }
        }

        protected override void OnHitEnemy(Collider2D other)
        {
            // 관통
        }

        public override void OnSpawnFromPool()
        {
            base.OnSpawnFromPool();
            phase = Phase.Outgoing;
            startPosition = Vector2.zero;
            hasPullOnReturn = false;
        }
    }
}
