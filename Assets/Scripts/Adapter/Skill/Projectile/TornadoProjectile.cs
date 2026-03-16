using UnityEngine;
using Photon.Pun;
using SwDreams.Domain.Interfaces;
using SwDreams.Adapter.Manager;

namespace SwDreams.Adapter.Skill
{
    /// <summary>
    /// 회오리 투사체. 회오리바람용.
    /// 느리게 전진하면서 범위 내 적을 매 프레임 조금씩 끌어당기고 틱 데미지.
    ///
    /// 끌어당김: 호스트에서만 처리 → PhotonTransformView가 위치 동기화.
    /// 데미지: 호스트에서만 처리.
    ///
    /// 프리팹: SpriteRenderer + CircleCollider2D(Trigger) + TornadoProjectile
    /// pullRadius, pullForce는 인스펙터에서도 조정 가능.
    /// </summary>
    public class TornadoProjectile : Projectile
    {
        // SkillData.pullRadius / pullForce에서 SetTornado()로 주입
        private float pullRadius = 2f;
        private float pullForce = 1.5f;

        private float tickRate = 0.3f;
        private float tickTimer;

        public void SetTornado(float pullRadius, float pullForce)
        {
            this.pullRadius = pullRadius;
            this.pullForce = pullForce;
            tickTimer = 0f;
        }

        protected override void MoveStep()
        {
            // 느린 직선 이동 (모든 클라이언트)
            transform.position += (Vector3)(direction * speed * Time.deltaTime);
            transform.Rotate(0, 0, 360f * Time.deltaTime);

            // 호스트만: 끌어당김 (매 프레임) + 데미지 (틱)
            if (!PhotonNetwork.IsMasterClient) return;

            PullEnemies();

            tickTimer += Time.deltaTime;
            if (tickTimer >= tickRate)
            {
                tickTimer -= tickRate;
                DamageEnemies();
            }
        }

        /// <summary>
        /// 매 프레임 소량 끌어당김. deltaTime 기반이라 자연스러움.
        /// 중심에 가까울수록 약하게 (뭉침 방지), 멀수록 강하게.
        /// </summary>
        private void PullEnemies()
        {
            var hits = Physics2D.OverlapCircleAll(transform.position, pullRadius);

            foreach (var hit in hits)
            {
                if (hit.GetComponent<Entity.Boss>() != null) continue;
                if (!hit.CompareTag("Enemy")) continue;
                if (!hit.gameObject.activeInHierarchy) continue;

                float dist = Vector2.Distance(transform.position, hit.transform.position);
                if (dist < 0.2f) continue; // 너무 가까우면 스킵 (뭉침 방지)

                // 거리 비례: 멀수록 강하게 끌어당김
                float ratio = Mathf.Clamp01(dist / pullRadius);
                float amount = pullForce * ratio * Time.deltaTime;

                hit.transform.position = Vector2.MoveTowards(
                    hit.transform.position,
                    transform.position,
                    amount
                );
            }
        }

        /// <summary>
        /// 틱 기반 데미지. 호스트에서만.
        /// </summary>
        private void DamageEnemies()
        {
            var hits = Physics2D.OverlapCircleAll(transform.position, pullRadius);

            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Enemy")) continue;

                var damageable = hit.GetComponent<IDamageable>();
                if (damageable != null && damageable.IsAlive)
                {
                    damageable.TakeDamage(damage);
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
            tickTimer = 0f;
        }
    }
}
