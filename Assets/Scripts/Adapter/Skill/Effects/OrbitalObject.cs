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
    /// 1. 플레이어 주변 원형 궤도 회전 (위치는 OrbitalEffect.Update()에서 제어)
    /// 2. OverlapCircleAll로 범위 내 적 판정 + 데미지 + 넉백 (호스트만)
    /// 3. duration 후 풀 반환
    ///
    /// 주의: OrbitalEffect가 transform.position을 직접 이동하므로
    /// OnTriggerStay2D가 발동하지 않음 → OverlapCircleAll 사용.
    ///
    /// 프리팹: SpriteRenderer + OrbitalObject
    /// (콜라이더 불필요 — OverlapCircleAll로 직접 탐지)
    /// </summary>
    public class OrbitalObject : MonoBehaviour, IPoolable
    {
        // 런타임 설정 (Initialize에서 주입)
        private int damage;
        private float knockbackForce;
        private float duration;
        private float aliveTime;
        private bool isActive;

        // 판정 반경 (스프라이트 크기 기준, 인스펙터에서 조정 가능)
        [SerializeField] private float hitRadius = 0.3f;

        // 히트 쿨다운 (같은 적에게 연속 히트 방지)
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
                GameManager.Instance.CurrentState != GameManager.GameState.Playing &&
                GameManager.Instance.CurrentState != GameManager.GameState.BossFight)
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
            {
                hitTimer -= Time.deltaTime;
                return; // 쿨다운 중이면 판정 스킵
            }

            // 호스트에서만 데미지 판정
            if (!PhotonNetwork.IsMasterClient) return;

            // OverlapCircle로 범위 내 적 탐색
            var hits = Physics2D.OverlapCircleAll(transform.position, hitRadius);
            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Enemy")) continue;

                var damageable = hit.GetComponent<IDamageable>();
                if (damageable != null && damageable.IsAlive)
                {
                    damageable.TakeDamage(damage);

                    // 넉백 적용
                    if (knockbackForce > 0f)
                        ApplyKnockback(hit);

                    // 히트 쿨다운 시작 (1회 판정 후 잠시 대기)
                    hitTimer = hitCooldown;
                    return; // 프레임당 1타겟만
                }
            }
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
