using UnityEngine;
using Photon.Pun;
using SwDreams.Domain.Interfaces;
using SwDreams.Adapter.Manager;

namespace SwDreams.Adapter.Skill
{
    /// <summary>
    /// 회전 오브젝트 개체. OrbitalEffect가 관리.
    ///
    /// 동작:
    /// 1. 플레이어 주변 원형 궤도 회전
    /// 2. 적과 접촉 시 데미지 + 넉백 (호스트만)
    /// 3. duration 후 풀 반환
    ///
    /// OrbitalEffect가 위치/회전을 직접 제어하므로
    /// 이 컴포넌트는 충돌 판정 + 생명주기만 담당.
    ///
    /// 프리팹: SpriteRenderer + CircleCollider2D(Trigger) + OrbitalObject
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class OrbitalObject : MonoBehaviour, IPoolable
    {
        // 런타임 설정 (Initialize에서 주입)
        private int damage;
        private float knockbackForce;
        private float duration;
        private float aliveTime;
        private bool isActive;

        // 개별 히트 쿨다운 (같은 적에게 연속 히트 방지)
        private float hitCooldown = 0.3f;
        private float hitTimer;

        /// <summary>
        /// OrbitalEffect에서 생성 후 호출.
        /// </summary>
        public void Initialize(int damage, float knockbackForce, float duration)
        {
            this.damage = damage;
            this.knockbackForce = knockbackForce;
            this.duration = duration;

            aliveTime = 0f;
            hitTimer = 0f;
            isActive = true;
        }

        private void Update()
        {
            if (!isActive) return;

            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentState != GameManager.GameState.Playing)
                return;

            // 수명 체크
            aliveTime += Time.deltaTime;
            if (aliveTime >= duration)
            {
                ReturnToPool();
                return;
            }

            // 히트 쿨다운 감소
            if (hitTimer > 0f)
                hitTimer -= Time.deltaTime;
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (!isActive) return;
            if (!other.CompareTag("Enemy")) return;
            if (hitTimer > 0f) return;

            // 호스트에서만 데미지 적용
            if (PhotonNetwork.IsMasterClient)
            {
                var damageable = other.GetComponent<IDamageable>();
                if (damageable != null && damageable.IsAlive)
                {
                    damageable.TakeDamage(damage);

                    // 넉백 적용
                    if (knockbackForce > 0f)
                    {
                        ApplyKnockback(other);
                    }
                }
            }

            hitTimer = hitCooldown;
        }

        private void ApplyKnockback(Collider2D enemyCollider)
        {
            var rb = enemyCollider.GetComponent<Rigidbody2D>();
            if (rb == null) return;

            Vector2 direction = (enemyCollider.transform.position - transform.position).normalized;

            // 넉백 저항 적용
            float resistance = 0f;
            var enemy = enemyCollider.GetComponent<Entity.Enemy>();
            if (enemy != null)
                resistance = enemy.KnockbackResistance;

            float finalForce = knockbackForce * (1f - resistance);
            rb.AddForce(direction * finalForce, ForceMode2D.Impulse);
        }

        private void ReturnToPool()
        {
            isActive = false;
            PoolManager.Instance?.Return(gameObject);
        }

        // ===== IPoolable =====

        public void OnSpawnFromPool()
        {
            gameObject.SetActive(true);
            aliveTime = 0f;
            hitTimer = 0f;
            isActive = true;
        }

        public void OnReturnToPool()
        {
            isActive = false;
            gameObject.SetActive(false);
        }
    }
}
