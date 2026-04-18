using UnityEngine;
using SwDreams.Features.Boss.Adapter;
using SwDreams.Features.Character.Adapter;
using SwDreams.Features.Skill.Domain.ValueObjects;
using SwDreams.Features.Skill.Adapter;
using Photon.Pun;
using SwDreams.Shared.Domain.Interfaces;
using SwDreams.Shared.Managers;
using SwDreams.Features.Skill.Adapter.TriggerEffects;

namespace SwDreams.Features.Skill.Adapter
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

        // 타이머
        private float aliveTime;
        private float tickTimer;
        private bool isActive;
        private bool hasTicked;

        /// <summary>
        /// 최소 1회 데미지 틱이 발동했는지. 
        /// AreaSpawner가 maxInstances 초과 시 제거 대상 판별에 사용.
        /// </summary>
        public bool HasTicked => hasTicked;

        // 캐시
        private SpriteRenderer spriteRenderer;

        // [Step 3-5] Trigger+Effect 시스템 연결
        private SkillTriggerSystem triggerSystem;
        private Transform ownerTransform;

        // 소유자 판별 (C안 데미지 요청)
        private bool isLocalPlayerOwned;
        private int ownerActorNumber = -1;

        /// <summary>AreaSpawner에서 스폰 후 호출. TriggerSystem 연결 + 소유자 판별.</summary>
        public void SetTriggerSystem(SkillTriggerSystem system, Transform owner)
        {
            triggerSystem = system;
            ownerTransform = owner;

            isLocalPlayerOwned = false;
            ownerActorNumber = -1;
            if (owner != null)
            {
                var pv = owner.GetComponent<PhotonView>();
                if (pv != null)
                {
                    isLocalPlayerOwned = pv.IsMine;
                    ownerActorNumber = pv.Owner != null ? pv.Owner.ActorNumber : -1;
                }
            }
        }

        private void Awake()
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        /// <summary>
        /// AreaEffect에서 스폰 후 호출.
        /// </summary>
        public void Initialize(Vector2 position, int damage, float radius,
            float duration, float tickRate, bool isHealing)
        {
            transform.position = position;
            this.damage = damage;
            this.radius = radius;
            this.duration = duration;
            this.tickRate = Mathf.Max(0.1f, tickRate);
            this.isHealing = isHealing;

            aliveTime = 0f;
            tickTimer = 0f;
            hasTicked = false;
            isActive = true;

            // 비주얼 크기 조정 — 스프라이트 실제 크기 기준으로 스케일 계산
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                float spriteWorldSize = spriteRenderer.sprite.bounds.size.x; // 스프라이트 1x 스케일 시 크기
                float desiredDiameter = radius * 2f;
                float scale = desiredDiameter / spriteWorldSize;
                transform.localScale = new Vector3(scale, scale, 1f);
            }
            else
            {
                // fallback: 스프라이트 없으면 1x1 기준
                float visualScale = radius * 2f;
                transform.localScale = new Vector3(visualScale, visualScale, 1f);
            }

            Debug.Log($"[AreaZone] 생성 — pos:{position}, radius:{radius}, " +
                      $"duration:{duration}, tick:{tickRate}, healing:{isHealing}");
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

            // 틱 판정: 자기 장판이면 클라이언트에서도 실행 (C안)
            // 남의 장판은 호스트에서만 실행
            if (!isLocalPlayerOwned && !PhotonNetwork.IsMasterClient) return;

            tickTimer += Time.deltaTime;
            if (tickTimer >= tickRate)
            {
                tickTimer -= tickRate;
                ApplyTick();
            }
        }

        /// <summary>
        /// 틱 판정. 범위 내 대상에게 효과 적용.
        /// 회복 장판이라도 OnHit 트리거가 있으면 적에게도 판정 (심판의 성역).
        /// </summary>
        private void ApplyTick()
        {
            hasTicked = true;

            if (isHealing)
                ApplyHealTick();

            // 피해 장판이거나, 회복 장판이라도 OnHit 트리거가 있으면 적 판정
            if (!isHealing || (triggerSystem != null && triggerSystem.HasTrigger(TriggerType.OnHit)))
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

                // Enemy 컴포넌트는 ShowHitVisuals/EnemyId용 (Boss는 null)
                var enemy = hit.GetComponent<SwDreams.Features.Enemy.Adapter.Enemy>();

                if (isLocalPlayerOwned)
                {
                    if (PhotonNetwork.IsMasterClient)
                    {
                        if (enemy != null) enemy.LastDamagerActorNumber = ownerActorNumber;
                        damageable.TakeDamage(damage);
                    }
                    else if (enemy != null)
                    {
                        enemy.ShowHitVisuals(damage);
                        SpawnManager.Instance?.RequestDamage(
                            enemy.EnemyId, damage, ownerActorNumber);
                    }
                    else
                    {
                        // Boss: PhotonView RPC로 직접 데미지 요청
                        var boss = hit.GetComponent<SwDreams.Features.Boss.Adapter.Boss>();
                        if (boss != null)
                            boss.RequestDamageFromClient(damage);
                    }
                }
                else
                {
                    // 남의 장판 (호스트에서만 여기 도달): 직접 데미지
                    if (enemy != null) enemy.LastDamagerActorNumber = ownerActorNumber;
                    damageable.TakeDamage(damage);
                }

                // OnHit 트리거 발동
                if (triggerSystem != null && triggerSystem.HasTrigger(TriggerType.OnHit))
                {
                    triggerSystem.FireTrigger(TriggerType.OnHit, new TriggerContext
                    {
                        position = hit.transform.position,
                        target = hit.transform,
                        damage = damage,
                        owner = ownerTransform
                    });
                }
            }
        }

        private void ApplyHealTick()
        {
            var hits = Physics2D.OverlapCircleAll(transform.position, radius);

            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Player")) continue;

                var health = hit.GetComponent<SwDreams.Features.Character.Adapter.PlayerHealth>();
                if (health == null || !health.IsAlive) continue;

                // 풀피면 스킵 (불필요한 RPC 방지)
                if (health.CurrentHP >= health.MaxHP) continue;

                health.Heal(damage);
            }
        }

        private void ReturnToPool()
        {
            // [Step 3-5] OnExpire 트리거 발동
            if (triggerSystem != null && triggerSystem.HasTrigger(TriggerType.OnExpire)
                && Photon.Pun.PhotonNetwork.IsMasterClient)
            {
                triggerSystem.FireTrigger(TriggerType.OnExpire, new TriggerContext
                {
                    position = transform.position,
                    damage = damage,
                    owner = ownerTransform
                });
            }

            isActive = false;
            triggerSystem = null;
            ownerTransform = null;
            PoolManager.Instance?.Return(gameObject);
        }

        // ===== IPoolable =====

        public void OnSpawnFromPool()
        {
            gameObject.SetActive(true);
            aliveTime = 0f;
            tickTimer = 0f;
            hasTicked = false;
            isActive = true;
        }

        public void OnReturnToPool()
        {
            isActive = false;
            hasTicked = false;
            triggerSystem = null;
            ownerTransform = null;
            isLocalPlayerOwned = false;
            ownerActorNumber = -1;
            gameObject.SetActive(false);
        }
    }
}