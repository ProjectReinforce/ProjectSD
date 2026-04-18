using UnityEngine;
using SwDreams.Features.Skill.Adapter.Trajectories;
using Photon.Pun;
using SwDreams.Shared.Domain.Interfaces;

namespace SwDreams.Features.Skill.Adapter.Trajectories
{
    /// <summary>
    /// 회오리 궤적. 느린 직선 + 범위 흡인 + 틱 데미지. 관통.
    /// 기존 TornadoProjectile 로직 포팅.
    /// </summary>
    public class TornadoTrajectory : ITrajectoryBehavior
    {
        protected float pullRadius;
        protected float pullForce;
        protected float tickRate;
        protected float tickTimer;

        public bool Penetrates => true;
        public virtual bool OverridesLifetime => false;

        public TornadoTrajectory(float pullRadius = 2f, float pullForce = 1.5f, float tickRate = 0.3f)
        {
            this.pullRadius = pullRadius;
            this.pullForce = pullForce;
            this.tickRate = tickRate;
        }

        public virtual void Initialize(Projectile projectile)
        {
            tickTimer = 0f;
        }

        public virtual void Reset()
        {
            tickTimer = 0f;
        }

        public virtual void UpdateMovement(Projectile projectile, float deltaTime)
        {
            // 느린 직선 이동
            projectile.transform.position += (Vector3)(projectile.Direction * projectile.Speed * deltaTime);
            projectile.transform.Rotate(0, 0, 360f * deltaTime);

            if (!PhotonNetwork.IsMasterClient) return;

            PullEnemies(projectile.transform.position, deltaTime);

            tickTimer += deltaTime;
            if (tickTimer >= tickRate)
            {
                tickTimer -= tickRate;
                DamageEnemies(projectile.transform.position, projectile.Damage);
            }
        }

        protected void PullEnemies(Vector2 center, float deltaTime)
        {
            var hits = Physics2D.OverlapCircleAll(center, pullRadius);
            foreach (var hit in hits)
            {
                // 보스 제외
                if (hit.GetComponent<SwDreams.Features.Boss.Adapter.Boss>() != null) continue;
                if (!hit.CompareTag("Enemy")) continue;
                if (!hit.gameObject.activeInHierarchy) continue;

                float dist = Vector2.Distance(center, hit.transform.position);
                if (dist < 0.2f) continue;

                float ratio = Mathf.Clamp01(dist / pullRadius);
                float amount = pullForce * ratio * deltaTime;
                hit.transform.position = Vector2.MoveTowards(
                    hit.transform.position, center, amount);
            }
        }

        protected void DamageEnemies(Vector2 center, int damage)
        {
            var hits = Physics2D.OverlapCircleAll(center, pullRadius);
            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Enemy")) continue;
                var damageable = hit.GetComponent<IDamageable>();
                if (damageable != null && damageable.IsAlive)
                    damageable.TakeDamage(damage);
            }
        }
    }

    /// <summary>
    /// 나선 궤적. 고정 원점 기준 나선 확장 + 흡인 + 틱 데미지. 관통.
    /// 기존 SpiralTornadoProjectile 로직 포팅.
    /// </summary>
    public class SpiralTrajectory : TornadoTrajectory
    {
        private float expandSpeed;
        private float startAngle;
        private float angularSpeed;

        private Vector2 originPosition;
        private float currentAngle;
        private float currentRadius;
        private bool hasOrigin;

        public override bool OverridesLifetime => false;

        public SpiralTrajectory(float pullRadius = 2f, float pullForce = 1.5f,
            float expandSpeed = 1f, float startAngle = 0f,
            float angularSpeed = 180f, float tickRate = 0.3f)
            : base(pullRadius, pullForce, tickRate)
        {
            this.expandSpeed = expandSpeed;
            this.startAngle = startAngle;
            this.angularSpeed = angularSpeed;
        }

        /// <summary>원점 설정. ProjectileEffect에서 호출.</summary>
        public void SetOrigin(Vector2 origin)
        {
            originPosition = origin;
            hasOrigin = true;
        }

        public override void Initialize(Projectile projectile)
        {
            base.Initialize(projectile);
            currentAngle = startAngle;
            currentRadius = 0.5f;
            if (!hasOrigin)
                originPosition = projectile.transform.position;
            hasOrigin = true;
        }

        public override void Reset()
        {
            base.Reset();
            currentAngle = startAngle;
            currentRadius = 0.5f;
            hasOrigin = false;
        }

        public override void UpdateMovement(Projectile projectile, float deltaTime)
        {
            if (!hasOrigin)
            {
                projectile.ForceReturn();
                return;
            }

            currentAngle += angularSpeed * deltaTime;
            currentRadius += expandSpeed * deltaTime;

            float rad = currentAngle * Mathf.Deg2Rad;
            Vector2 offset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * currentRadius;
            projectile.transform.position = originPosition + offset;
            projectile.transform.Rotate(0, 0, 360f * deltaTime);

            if (!PhotonNetwork.IsMasterClient) return;

            PullEnemies(projectile.transform.position, deltaTime);

            tickTimer += deltaTime;
            if (tickTimer >= tickRate)
            {
                tickTimer -= tickRate;
                DamageEnemies(projectile.transform.position, projectile.Damage);
            }
        }
    }
}
