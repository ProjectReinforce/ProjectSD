using UnityEngine;
using Photon.Pun;
using SwDreams.Domain.Interfaces;
using SwDreams.Adapter.Manager;

namespace SwDreams.Adapter.Skill
{
    /// <summary>
    /// 자동포탑 오브젝트. PlacedEffect.Execute()에서 생성.
    ///
    /// 동작:
    /// 1. 플레이어 위치에 설치
    /// 2. attackRange 내 가장 가까운 적을 탐색
    /// 3. attackCooldown 간격으로 즉발 공격 (투사체 없는 직접 데미지)
    /// 4. duration 후 풀 반환
    ///
    /// alwaysCritical = true면 항상 치명타 데미지 적용.
    /// 네트워크: 로컬 비주얼, 호스트 데미지 판정.
    ///
    /// 프리팹: SpriteRenderer + PlacedTurret
    /// (콜라이더 불필요 — 직접 OverlapCircle 사용)
    /// </summary>
    public class PlacedTurret : MonoBehaviour, IPoolable
    {
        // 런타임 설정
        private int damage;
        private float attackRange;
        private float attackCooldown;
        private float duration;
        private bool alwaysCritical;
        private float critDamageMultiplier;

        // 타이머
        private float aliveTime;
        private float attackTimer;
        private bool isActive;

        // 비주얼
        private SpriteRenderer spriteRenderer;

        // 공격 대상 캐시 (매 프레임 탐색 방지)
        private Transform currentTarget;
        private float targetSearchTimer;
        private const float TARGET_SEARCH_INTERVAL = 0.2f;

        private void Awake()
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        /// <summary>
        /// PlacedEffect에서 스폰 후 호출.
        /// </summary>
        public void Initialize(Vector2 position, int damage, float attackRange,
            float attackCooldown, float duration, bool alwaysCritical,
            float critDamageMultiplier)
        {
            transform.position = position;
            this.damage = damage;
            this.attackRange = attackRange;
            this.attackCooldown = Mathf.Max(0.1f, attackCooldown);
            this.duration = duration;
            this.alwaysCritical = alwaysCritical;
            this.critDamageMultiplier = critDamageMultiplier;

            aliveTime = 0f;
            attackTimer = 0f;
            targetSearchTimer = 0f;
            currentTarget = null;
            isActive = true;

            Debug.Log($"[PlacedTurret] 설치 — pos:{position}, range:{attackRange}, " +
                      $"cd:{attackCooldown}, duration:{duration}, crit:{alwaysCritical}");
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

            // 대상 탐색 (모든 클라이언트 — 비주얼 회전용)
            targetSearchTimer += Time.deltaTime;
            if (targetSearchTimer >= TARGET_SEARCH_INTERVAL)
            {
                targetSearchTimer = 0f;
                FindTarget();
            }

            // 포탑 방향 전환 (비주얼)
            if (currentTarget != null && currentTarget.gameObject.activeInHierarchy)
            {
                Vector2 dir = currentTarget.position - transform.position;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0, 0, angle);
            }

            // 공격 (호스트만)
            if (!PhotonNetwork.IsMasterClient) return;

            attackTimer += Time.deltaTime;
            if (attackTimer >= attackCooldown)
            {
                attackTimer -= attackCooldown;
                TryAttack();
            }
        }

        private void FindTarget()
        {
            currentTarget = null;
            float minDist = float.MaxValue;

            var enemies = GameObject.FindGameObjectsWithTag("Enemy");
            foreach (var enemy in enemies)
            {
                if (!enemy.activeInHierarchy) continue;

                float dist = Vector2.Distance(transform.position, enemy.transform.position);
                if (dist <= attackRange && dist < minDist)
                {
                    minDist = dist;
                    currentTarget = enemy.transform;
                }
            }

            // 초기 디버그용 (확인 후 제거 가능)
            if (currentTarget != null && aliveTime < 2f)
                Debug.Log($"[PlacedTurret] 대상 발견: {currentTarget.name}, 거리:{minDist:F1}");
        }

        private void TryAttack()
        {
            if (currentTarget == null || !currentTarget.gameObject.activeInHierarchy)
            {
                FindTarget(); // 공격 시점에 재탐색
                if (currentTarget == null) return;
            }

            // 사거리 재확인
            float dist = Vector2.Distance(transform.position, currentTarget.position);
            if (dist > attackRange)
            {
                currentTarget = null;
                return;
            }

            var damageable = currentTarget.GetComponent<IDamageable>();
            if (damageable == null || !damageable.IsAlive) return;

            // 데미지 계산
            int finalDamage = damage;
            if (alwaysCritical)
            {
                finalDamage = Mathf.RoundToInt(damage * critDamageMultiplier);
            }

            damageable.TakeDamage(finalDamage);

            Debug.Log($"[PlacedTurret] 공격! 대상:{currentTarget.name}, " +
                      $"데미지:{finalDamage}{(alwaysCritical ? " (치명타)" : "")}");

            // TODO [Phase 5 비주얼]: 공격 라인 이펙트 (SpriteRenderer or LineRenderer)
        }

        private void ReturnToPool()
        {
            isActive = false;
            currentTarget = null;
            PoolManager.Instance?.Return(gameObject);
        }

        // ===== IPoolable =====

        public void OnSpawnFromPool()
        {
            gameObject.SetActive(true);
            aliveTime = 0f;
            attackTimer = 0f;
            targetSearchTimer = 0f;
            currentTarget = null;
            isActive = true;
        }

        public void OnReturnToPool()
        {
            isActive = false;
            currentTarget = null;
            gameObject.SetActive(false);
        }
    }
}
