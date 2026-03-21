using UnityEngine;
using Photon.Pun;
using SwDreams.Domain.Interfaces;
using SwDreams.Adapter.Manager;
using SwDreams.Adapter.Entity;

namespace SwDreams.Adapter.Skill
{
    /// <summary>
    /// 투사체 기본 클래스.
    /// 모든 클라이언트에서 로컬로 이동 + 렌더링.
    /// 히트 판정은 호스트에서만 처리.
    ///
    /// 서브클래스:
    ///   Projectile         → 표창 (직선)
    ///   HomingProjectile   → 매직 미사일 (유도)
    ///   BoomerangProjectile→ 부메랑 (왕복)
    ///   TornadoProjectile  → 회오리바람 (느린 전진 + 흡인)
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Projectile : MonoBehaviour, IPoolable
    {
        // 서브클래스에서 접근 가능하도록 protected
        protected Vector2 direction;
        protected float speed;
        protected int damage;
        protected float lifetime;
        protected float aliveTime;
        protected float knockbackForce;

        public virtual void Initialize(Vector2 position, Vector2 direction,
            int damage, float speed, float lifetime, float knockbackForce = 0f)
        {
            transform.position = position;
            this.direction = direction.normalized;
            // 방향이 zero면 기본값 (적과 플레이어가 완전히 겹칠 때)
            if (this.direction.sqrMagnitude < 0.01f)
                this.direction = Vector2.right;
            this.damage = damage;
            this.speed = speed;
            this.lifetime = lifetime;
            this.knockbackForce = knockbackForce;
            aliveTime = 0f;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }

        protected virtual void Update()
        {
            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentState != GameManager.GameState.Playing &&
                GameManager.Instance.CurrentState != GameManager.GameState.BossFight)
                return;

            MoveStep();

            aliveTime += Time.deltaTime;
            if (aliveTime >= lifetime)
                ReturnToPool();
        }

        /// <summary>
        /// 매 프레임 이동 처리. 서브클래스에서 오버라이드.
        /// </summary>
        protected virtual void MoveStep()
        {
            transform.position += (Vector3)(direction * speed * Time.deltaTime);
        }

        protected virtual void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Enemy")) return;
            
            if (PhotonNetwork.IsMasterClient)
            {
                var damageable = other.GetComponent<IDamageable>();
                if (damageable != null && damageable.IsAlive)
                {
                    damageable.TakeDamage(damage);

                    if (knockbackForce > 0f)
                    {
                        var enemy = other.GetComponent<Enemy>();
                        if (enemy != null)
                            enemy.ApplyKnockback(transform.position, knockbackForce);
                    }
                }
            }

            OnHitEnemy(other);
        }

        /// <summary>
        /// 적 히트 시 후처리. 기본: 풀 반환. 서브클래스에서 오버라이드 가능.
        /// (예: 부메랑은 관통, 회오리는 관통)
        /// </summary>
        protected virtual void OnHitEnemy(Collider2D other)
        {
            ReturnToPool();
        }

        protected void ReturnToPool()
        {
            PoolManager.Instance?.Return(gameObject);
        }

        /// <summary>
        /// 투사체 방향에 맞게 회전 갱신.
        /// </summary>
        protected void UpdateRotation(Vector2 dir)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }

        public virtual void OnSpawnFromPool()
        {
            gameObject.SetActive(true);
            aliveTime = 0f;
        }

        public virtual void OnReturnToPool()
        {
            gameObject.SetActive(false);
        }
    }
}