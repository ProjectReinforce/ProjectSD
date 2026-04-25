using System;
using SwDreams.Features.UI.Presentation;
using SwDreams.Features.Character.Adapter;
using SwDreams.Features.Enemy.Adapter.Data;
using SwDreams.Features.Enemy.Adapter;
using SwDreams.Features.Skill.Application;
using System.Collections;
using UnityEngine;
using SwDreams.Shared.Domain.Interfaces;
using SwDreams.Shared.Managers;
using SwDreams.Shared.Data;
using SwDreams.Features.Skill.Adapter;
using SwDreams.Features.Enemy.Adapter.Attack;

namespace SwDreams.Features.Enemy.Adapter
{
    /// <summary>
    /// 적 엔티티. 상태(HP) 관리 + 이벤트 발행.
    /// 이동은 EnemyMovement, 접촉 판정은 EnemyContact에서 처리.
    /// 
    /// PhotonView 없음. 네트워크 동기화는 SpawnManager가 RPC로 처리.
    /// 각 적은 고유 ID로 호스트-클라이언트 간 매칭.
    /// 
    /// Phase 3 변경:
    /// - EnemyType 노출 (EnemyMovement에서 전략 선택용)
    /// - KnockbackResistance (Tank의 넉백 감소)
    /// - ForceReturn() (Swarm 수명 만료 시 풀 반환)
    /// - OnForceReturned 이벤트 (경험치 드롭 없는 제거)
    /// </summary>
    [RequireComponent(typeof(EnemyMovement))]
    [RequireComponent(typeof(EnemyContact))]
    public class Enemy : MonoBehaviour, IDamageable, IPoolable
    {
        // 네트워크 식별용
        public int EnemyId { get; private set; }

        // 데이터
        private EnemyData enemyData;
        private DamageService damageService;

        // SO 참조 (중도 참가 시 variantIdx 역산에 사용)
        public EnemyData Data => enemyData;

        // 상태
        public int CurrentHP { get; private set; }
        public int MaxHP { get; private set; }
        public bool IsAlive => CurrentHP > 0;
        public int ExpValue => enemyData != null ? enemyData.expValue : 0;
        public float MoveSpeed => enemyData != null ? enemyData.moveSpeed : 0f;
        public int ContactDamage => enemyData != null ? enemyData.contactDamage : 0;

        // Phase 3: 타입 + 넉백 저항
        public EnemyType EnemyType => enemyData != null ? enemyData.enemyType : EnemyType.Chaser;
        public float KnockbackResistance => enemyData != null ? enemyData.knockbackResistance : 0f;
        public bool ResolveOverlap => enemyData == null || enemyData.resolveOverlap;

        // Phase B: 원거리형 데이터 (EnemyType.Ranged 에서만 의미 있음)
        public RangedBehavior RangedBehaviorType => enemyData != null ? enemyData.rangedBehavior : RangedBehavior.Stationary;
        public RangedAttack RangedAttackType => enemyData != null ? enemyData.rangedAttack : RangedAttack.Projectile;
        public float AttackRange => enemyData != null ? enemyData.attackRange : 0f;
        public float AttackInterval => enemyData != null ? enemyData.attackInterval : 0f;
        public int AttackDamage => enemyData != null ? enemyData.attackDamage : 0;
        public float ProjectileSpeed => enemyData != null ? enemyData.projectileSpeed : 0f;
        public float ProjectileLifetime => enemyData != null ? enemyData.projectileLifetime : 0f;
        public float TelegraphDuration => enemyData != null ? enemyData.telegraphDuration : 0f;
        public float TelegraphRadius => enemyData != null ? enemyData.telegraphRadius : 0f;

        // Phase C: 엘리트
        public bool IsElite => enemyData != null && enemyData.isElite;

        /// <summary>
        /// 마지막으로 데미지를 준 플레이어의 ActorNumber.
        /// 사망 시 연쇄폭발 등 킬러 귀속 효과에 사용.
        /// 데미지 소스에서 TakeDamage 호출 전에 설정.
        /// </summary>
        public int LastDamagerActorNumber { get; set; } = -1;

        // 이벤트
        public event Action<int, int> OnHealthChanged;
        public event Action OnDied;
        public event Action<Enemy> OnDiedWithRef;

        /// <summary>
        /// Swarm 수명 만료 등 사망이 아닌 제거 시 발생.
        /// SpawnManager에서 구독하여 activeEnemies에서 제거.
        /// </summary>
        public event Action<Enemy> OnForceReturned;

        // 컴포넌트 캐시
        private SpriteRenderer spriteRenderer;
        private EnemyAttack enemyAttack;

        // 프리팹 기본 스케일 (visualScaleMultiplier 적용 시 기준)
        private Vector3 initialLocalScale;

        // 피격 플래시
        private static readonly Color HitFlashColor = new Color(1f, 0.4f, 0.4f, 1f); // 붉은 틴트
        private const float HitFlashDuration = 0.1f;
        private Coroutine hitFlashCoroutine;
        private MaterialPropertyBlock mpb;
        private Color originalColor = Color.white;

        private void Awake()
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            mpb = new MaterialPropertyBlock();
            enemyAttack = GetComponent<EnemyAttack>();
            initialLocalScale = transform.localScale;
        }

        /// <summary>
        /// 스폰 시 초기화. SpawnManager에서 호출.
        /// </summary>
        public void Initialize(int id, EnemyData data, Vector2 position,
            DamageService dmgService, float hpMultiplier = 1f)
        {
            EnemyId = id;
            enemyData = data;
            damageService = dmgService;

            MaxHP = Mathf.RoundToInt(data.baseHP * hpMultiplier);
            CurrentHP = MaxHP;
            transform.position = position;
            gameObject.tag = "Enemy";

            // SO 의 visualScaleMultiplier 반영 (엘리트는 보통 >1 로 커 보이게)
            float mul = data.visualScaleMultiplier > 0.0001f ? data.visualScaleMultiplier : 1f;
            transform.localScale = initialLocalScale * mul;

            if (spriteRenderer != null && data.sprite != null)
                spriteRenderer.sprite = data.sprite;

            // 원래 색상 저장 (피격 플래시 복귀용)
            if (spriteRenderer != null)
                originalColor = spriteRenderer.color;

            GetComponent<EnemyMovement>().Initialize(this);
            GetComponent<EnemyContact>().Initialize(this);

            // Ranged 타입만 공격 사이클 활성화 (부착 안 돼 있으면 no-op)
            if (enemyAttack != null)
            {
                bool isRanged = data.enemyType == EnemyType.Ranged;
                enemyAttack.enabled = isRanged;
                if (isRanged) enemyAttack.ConfigureFromEnemy();
            }
        }

        public void TakeDamage(int damage)
        {
            if (!IsAlive) return;

            // [Phase 5] DebuffMark 추가 피해 적용
            var debuff = GetComponent<DebuffMark>();
            if (debuff != null)
                damage = Mathf.RoundToInt(damage * debuff.DamageAmplify);

            var result = damageService.ProcessSkillAttack(damage);
            CurrentHP = Mathf.Max(0, CurrentHP - result.FinalDamage);
            OnHealthChanged?.Invoke(CurrentHP, MaxHP);

            // 피격 플래시 (모든 클라이언트)
            TriggerHitFlash();

            // 데미지 숫자 팝업 (모든 클라이언트)
            DamagePopup.Spawn(transform.position, result.FinalDamage);

            // 피격 파티클 이펙트 (모든 클라이언트)
            HitEffect.Spawn(transform.position);

            if (!IsAlive)
                Die();
        }

        /// <summary>
        /// 클라이언트용 비주얼 피드백만 재생 (HP 변경 없음).
        /// 클라이언트에서 자기 투사체가 적에게 적중했을 때 호출.
        /// 데미지 팝업, 히트 플래시, 히트 이펙트를 표시.
        /// 
        /// 실제 HP 감소와 사망 판정은 호스트의 TakeDamage()에서만 처리.
        /// </summary>
        public void ShowHitVisuals(int displayDamage)
        {
            TriggerHitFlash();
            DamagePopup.Spawn(transform.position, displayDamage);
            HitEffect.Spawn(transform.position);
        }

        // ===== 피격 플래시 =====

        private void TriggerHitFlash()
        {
            if (spriteRenderer == null) return;

            if (hitFlashCoroutine != null)
                StopCoroutine(hitFlashCoroutine);
            hitFlashCoroutine = StartCoroutine(HitFlashRoutine());
        }

        private IEnumerator HitFlashRoutine()
        {
            spriteRenderer.color = HitFlashColor;
            yield return new WaitForSeconds(HitFlashDuration);
            spriteRenderer.color = originalColor;
            hitFlashCoroutine = null;
        }

        /// <summary>
        /// 외부 데미지 소스에서 넉백 적용.
        /// 호스트에서만 호출. KnockbackResistance 반영.
        /// </summary>
        /// <param name="sourcePos">데미지 소스 위치 (넉백 방향 계산용)</param>
        /// <param name="force">기본 넉백 힘</param>
        public void ApplyKnockback(Vector2 sourcePos, float force)
        {
            if (!IsAlive || force <= 0f) return;

            float finalForce = force * (1f - KnockbackResistance);
            if (finalForce <= 0f) return;

            Vector2 dir = ((Vector2)transform.position - sourcePos).normalized;
            if (dir.sqrMagnitude < 0.001f)
                dir = UnityEngine.Random.insideUnitCircle.normalized;

            var movement = GetComponent<EnemyMovement>();
            if (movement != null)
                movement.ApplyKnockback(dir * finalForce);
        }

        private void Die()
        {
            OnDied?.Invoke();
            OnDiedWithRef?.Invoke(this);
        }

        /// <summary>
        /// 사망이 아닌 강제 제거 (Swarm 수명 만료, 화면 밖 정리 등).
        /// 경험치 드롭 없이 풀에 반환.
        /// </summary>
        public void ForceReturn()
        {
            if (!IsAlive) return;
            CurrentHP = 0; // 이중 처리 방지
            OnForceReturned?.Invoke(this);
        }

        // === IPoolable ===
        public void OnSpawnFromPool()
        {
            gameObject.SetActive(true);
        }

        public void OnReturnToPool()
        {
            // 피격 플래시 리셋
            if (hitFlashCoroutine != null)
            {
                StopCoroutine(hitFlashCoroutine);
                hitFlashCoroutine = null;
            }
            if (spriteRenderer != null)
                spriteRenderer.color = originalColor;

            if (enemyAttack != null)
                enemyAttack.enabled = false;

            // 스케일 리셋 — 다음 스폰 전 잔상 방지
            transform.localScale = initialLocalScale;

            // Rigidbody2D BodyType reset — 격리 몹용 Kinematic 셋업이 다음 일반 적 사용 시 영향 안 미치도록.
            var rb = GetComponent<Rigidbody2D>();
            if (rb != null) rb.bodyType = RigidbodyType2D.Dynamic;
            // EnemyMovement 도 reset — 격리 몹 사용 시 비활성화했던 게 일반 적 재사용 시 영향 안 미치도록.
            var movement = GetComponent<EnemyMovement>();
            if (movement != null) movement.enabled = true;

            OnDied = null;
            OnDiedWithRef = null;
            OnHealthChanged = null;
            OnForceReturned = null;
            CurrentHP = 0;
            EnemyId = -1;
            LastDamagerActorNumber = -1;
            gameObject.SetActive(false);
        }
    }
}