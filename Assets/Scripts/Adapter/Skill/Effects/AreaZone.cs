using UnityEngine;
using Photon.Pun;
using SwDreams.Domain.Interfaces;
using SwDreams.Adapter.Manager;
using SwDreams.Adapter.Entity;

namespace SwDreams.Adapter.Skill
{
    /// <summary>
    /// 장판(지대) 오브젝트. AreaEffect.Execute()에서 생성.
    ///
    /// 동작:
    /// 1. 플레이어 위치에 스폰
    /// 2. tickRate 간격으로 범위 내 판정
    ///    - 피해 장판: 범위 내 적에게 데미지 (호스트만)
    ///    - 회복 장판: 범위 내 아군에게 회복 (호스트만)
    /// 3. duration 후 풀 반환
    ///
    /// 네트워크: 로컬 비주얼, 호스트 판정.
    /// 프리팹: SpriteRenderer + AreaZone
    /// (콜라이더 불필요 — OverlapCircleAll로 직접 탐지)
    /// </summary>
    public class AreaZone : MonoBehaviour, IPoolable
    {
        // 런타임 설정 (Initialize에서 주입)
        private int damage;
        private float duration;
        private float tickRate;
        private float radius;
        private bool isHealing;

        // [Phase 5 진화] 추가 효과
        private bool appliesSlow;
        private float slowMultiplier;
        private float executeThreshold; // 0이면 비활성
        private bool isDualZone;        // 적 데미지 + 플레이어 회복 동시

        // 타이머
        private float aliveTime;
        private float tickTimer;
        private bool isActive;

        // 캐시
        private SpriteRenderer spriteRenderer;

        private void Awake()
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        /// <summary>
        /// AreaEffect에서 스폰 후 호출.
        /// </summary>
        public void Initialize(Vector2 position, int damage, float radius,
            float duration, float tickRate, bool isHealing,
            bool appliesSlow = false, float slowMultiplier = 0.5f,
            float executeThreshold = 0f, bool isDualZone = false)
        {
            transform.position = position;
            this.damage = damage;
            this.radius = radius;
            this.duration = duration;
            this.tickRate = Mathf.Max(0.1f, tickRate);
            this.isHealing = isHealing;
            this.appliesSlow = appliesSlow;
            this.slowMultiplier = slowMultiplier;
            this.executeThreshold = executeThreshold;
            this.isDualZone = isDualZone;

            aliveTime = 0f;
            tickTimer = 0f;
            isActive = true;

            // 비주얼 크기 조정 (기본 스프라이트가 1x1 단위 기준)
            float visualScale = radius * 2f;
            transform.localScale = new Vector3(visualScale, visualScale, 1f);

            Debug.Log($"[AreaZone] 생성 — pos:{position}, radius:{radius}, " +
                      $"duration:{duration}, tick:{tickRate}, healing:{isHealing}, dmg:{damage}");
        }

        private void Update()
        {
            if (!isActive) return;

            // 게임 일시정지 시 정지
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

            // 틱 판정 (호스트만)
            if (!PhotonNetwork.IsMasterClient) return;

            tickTimer += Time.deltaTime;
            if (tickTimer >= tickRate)
            {
                tickTimer -= tickRate;
                ApplyTick();
            }
        }

        /// <summary>
        /// 틱 판정. 범위 내 대상에게 효과 적용.
        /// </summary>
        private void ApplyTick()
        {
            if (isDualZone)
            {
                ApplyDamageTick();
                ApplyHealTick();
            }
            else if (isHealing)
                ApplyHealTick();
            else
                ApplyDamageTick();
        }

        private void ApplyDamageTick()
        {
            var hits = Physics2D.OverlapCircleAll(transform.position, radius);

            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Enemy")) continue;

                var damageable = hit.GetComponent<IDamageable>();
                if (damageable == null || !damageable.IsAlive) continue;

                // [진화: 나락] HP 비율 이하 적 즉사 (보스 제외)
                if (executeThreshold > 0f)
                {
                    float hpRatio = (float)damageable.CurrentHP / Mathf.Max(1, damageable.MaxHP);
                    if (hpRatio <= executeThreshold)
                    {
                        // 즉사 = 현재 HP만큼 데미지
                        damageable.TakeDamage(damageable.CurrentHP);
                        continue;
                    }
                }

                // 일반 데미지
                damageable.TakeDamage(damage);

                // [진화: 뇌전역] 슬로우 — 이동속도 감소
                if (appliesSlow)
                {
                    var movement = hit.GetComponent<EnemyMovement>();
                    if (movement != null)
                        movement.ApplySlowTemporary(slowMultiplier, tickRate * 1.5f);
                }
            }
        }

        private void ApplyHealTick()
        {
            var hits = Physics2D.OverlapCircleAll(transform.position, radius);

            if (hits.Length == 0)
            {
                Debug.Log($"[AreaZone] 회복 틱 — 감지 콜라이더 0개 (pos:{transform.position}, radius:{radius})");
                return;
            }

            bool foundPlayer = false;
            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Player")) continue;
                foundPlayer = true;

                var damageable = hit.GetComponent<IDamageable>();
                if (damageable == null)
                {
                    Debug.LogWarning($"[AreaZone] {hit.name}에 IDamageable 없음!");
                    continue;
                }

                if (!damageable.IsAlive) continue;

                int hpBefore = damageable.CurrentHP;

                // 풀피면 스킵 (불필요한 RPC 방지)
                if (hpBefore >= damageable.MaxHP)
                    continue;

                // 음수 데미지 = 회복
                // TODO [Phase 5]: Player에 Heal(int) 메서드 추가 후 교체
                damageable.TakeDamage(-damage);
                Debug.Log($"[AreaZone] 회복! HP:{hpBefore}→{damageable.CurrentHP}/{damageable.MaxHP} (회복량:{damage})");
            }

            if (!foundPlayer)
                Debug.Log($"[AreaZone] 회복 틱 — 콜라이더 {hits.Length}개 감지했으나 Player 태그 없음");
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
            tickTimer = 0f;
            isActive = true;
        }

        public void OnReturnToPool()
        {
            isActive = false;
            gameObject.SetActive(false);
        }
    }
}
