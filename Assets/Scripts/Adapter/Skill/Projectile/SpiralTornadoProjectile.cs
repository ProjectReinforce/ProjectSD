using UnityEngine;
using Photon.Pun;
using SwDreams.Domain.Interfaces;
using SwDreams.Adapter.Manager;

namespace SwDreams.Adapter.Skill
{
    /// <summary>
    /// 나선형 회오리. 대선풍 진화용.
    /// 발사 지점을 중심으로 나선형으로 점점 멀어지며 회전.
    /// TornadoProjectile과 동일하게 끌어당김 + 지속 데미지.
    /// </summary>
    public class SpiralTornadoProjectile : Projectile
    {
        private float pullRadius = 2f;
        private float pullForce = 1.5f;
        private float spiralExpandSpeed = 1f;
        private float tickRate = 0.3f;
        private float tickTimer;

        // 나선 상태
        private Vector2 originPosition; // 발사 시점의 고정 원점
        private bool hasOrigin;
        private float currentAngle;
        private float currentRadius;
        private float angularSpeed = 180f; // 도/초

        public void SetSpiral(Transform player, float pullRadius, float pullForce,
            float expandSpeed, float startAngle = 0f)
        {
            // 플레이어 현재 위치를 고정 원점으로 저장 (이후 따라가지 않음)
            originPosition = player != null ? (Vector2)player.position : (Vector2)transform.position;
            hasOrigin = true;
            this.pullRadius = pullRadius;
            this.pullForce = pullForce;
            spiralExpandSpeed = expandSpeed;
            currentAngle = startAngle;
            currentRadius = 0.5f;
            tickTimer = 0f;
        }

        protected override void MoveStep()
        {
            if (!hasOrigin)
            {
                ReturnToPool();
                return;
            }

            // 나선형 이동: 각도 증가 + 반경 확장
            currentAngle += angularSpeed * Time.deltaTime;
            currentRadius += spiralExpandSpeed * Time.deltaTime;

            float rad = currentAngle * Mathf.Deg2Rad;
            Vector2 offset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * currentRadius;
            transform.position = originPosition + offset;

            // 회전 연출
            transform.Rotate(0, 0, 360f * Time.deltaTime);

            // 호스트만: 끌어당김 + 데미지
            if (!PhotonNetwork.IsMasterClient) return;

            PullEnemies();

            tickTimer += Time.deltaTime;
            if (tickTimer >= tickRate)
            {
                tickTimer -= tickRate;
                DamageEnemies();
            }
        }

        private void PullEnemies()
        {
            var hits = Physics2D.OverlapCircleAll(transform.position, pullRadius);
            foreach (var hit in hits)
            {
                if (hit.GetComponent<SwDreams.Adapter.Entity.Boss>() != null) continue;
                if (!hit.CompareTag("Enemy")) continue;
                if (!hit.gameObject.activeInHierarchy) continue;

                float dist = Vector2.Distance(transform.position, hit.transform.position);
                if (dist < 0.2f) continue;

                float ratio = Mathf.Clamp01(dist / pullRadius);
                float amount = pullForce * ratio * Time.deltaTime;

                hit.transform.position = Vector2.MoveTowards(
                    hit.transform.position,
                    transform.position,
                    amount
                );
            }
        }

        private void DamageEnemies()
        {
            var hits = Physics2D.OverlapCircleAll(transform.position, pullRadius);
            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Enemy")) continue;
                var damageable = hit.GetComponent<IDamageable>();
                if (damageable != null && damageable.IsAlive)
                    damageable.TakeDamage(damage);
            }
        }

        protected override void OnHitEnemy(Collider2D other)
        {
            // 관통
        }

        public override void OnSpawnFromPool()
        {
            base.OnSpawnFromPool();
            currentAngle = 0f;
            currentRadius = 0.5f;
            tickTimer = 0f;
            hasOrigin = false;
        }
    }
}