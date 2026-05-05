using System;
using SwDreams.Features.UI.Presentation;
using SwDreams.Features.Character.Adapter;
using SwDreams.Features.Boss.Domain;
using SwDreams.Features.Boss.Application;
using SwDreams.Features.Boss.Adapter.Data;
using SwDreams.Features.Boss.Adapter;
using UnityEngine;
using Photon.Pun;
using SwDreams.Shared.Domain;
using SwDreams.Shared.Domain.Interfaces;
using SwDreams.Shared.Data;
using SwDreams.Shared.Managers;

namespace SwDreams.Features.Boss.Adapter
{
    /// <summary>
    /// 보스 엔티티. IDamageable 구현.
    /// Enemy를 상속하지 않음 (IPoolable 불필요, 페이즈/공격 로직이 다름).
    ///
    /// 호스트: 이동 + 데미지 판정 + 페이즈 전환
    /// 클라이언트: PhotonTransformView로 위치 동기화 + 비주얼
    ///
    /// 프리팹 구성:
    /// - Boss (이 스크립트)
    /// - PhotonView + PhotonTransformView
    /// - Rigidbody2D (Gravity 0, Freeze Rotation Z)
    /// - CircleCollider2D (isTrigger = true, Enemy 레이어)
    /// - SpriteRenderer
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class Boss : MonoBehaviourPun, IDamageable, IEnemyEntity
    {
        [SerializeField] private BossData bossData;

        // ===== 상태 =====
        public int CurrentHP { get; private set; }
        public int MaxHP { get; private set; }
        public bool IsAlive => CurrentHP > 0;
        public BossData Data => bossData;
        public BossPhase CurrentPhase { get; private set; } = BossPhase.Phase1;

        // ===== 이벤트 =====
        public event Action<int, int> OnHealthChanged;  // current, max
        public event Action OnDied;
        public event Action<BossPhase> OnPhaseChanged;

        // ===== 이동 =====
        private float currentMoveSpeed;
        private Transform currentTarget;

        // ===== 접촉 데미지 =====
        private float contactCooldown = 0.5f;
        private float contactTimer;

        // Phase 7: HP RPC 쓰로틀링 (토네이도 고빈도 히트 대응)
        private float hpSyncInterval = 0.1f;
        private float hpSyncTimer = 0f;
        private bool hpDirty = false;

        // 컴포넌트 캐시
        private SpriteRenderer spriteRenderer;
        private BossAnimator bossAnimator;

        private void Awake()
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            bossAnimator = GetComponent<BossAnimator>(); // 옵션 — 없으면 정적 sprite
        }

        /// <summary>
        /// 보스 초기화. BossSpawner에서 호출.
        /// </summary>
        public void Initialize(BossData data, int playerCount)
        {
            bossData = data;

            // 인원수 스케일링
            var phaseService = new Application.BossPhaseService();
            MaxHP = phaseService.CalculateScaledHP(data.baseHP, playerCount, data.hpMultiplier);
            CurrentHP = MaxHP;

            currentMoveSpeed = data.moveSpeed;
            CurrentPhase = BossPhase.Phase1;

            gameObject.tag = "Enemy"; // 기존 스킬 시스템의 적 판정 재사용

            if (spriteRenderer != null && data.sprite != null)
                spriteRenderer.sprite = data.sprite;

            // BossAnimator 바인딩 (controller 주입 + OnDied 구독). 컴포넌트 없으면 no-op.
            if (bossAnimator != null)
                bossAnimator.Bind(this, data);

            Debug.Log($"[Boss] 초기화 — {data.bossName}, HP:{MaxHP} ({playerCount}인 스케일링)");
        }

        /// <summary>
        /// 클라이언트 전용 초기화. BossSpawner.RPC_InitBoss에서 호출.
        /// 호스트가 계산한 MaxHP를 받아서 HP 상태만 설정.
        /// bossData는 프리팹에 직렬화된 값 사용.
        /// </summary>
        public void InitializeFromNetwork(int maxHP)
        {
            MaxHP = maxHP;
            CurrentHP = maxHP;
            CurrentPhase = BossPhase.Phase1;
            gameObject.tag = "Enemy";

            if (bossData != null)
            {
                currentMoveSpeed = bossData.moveSpeed;
                if (spriteRenderer != null && bossData.sprite != null)
                    spriteRenderer.sprite = bossData.sprite;

                // BossAnimator 바인딩 — 클라 측도 controller 주입 + OnDied 구독 필요.
                if (bossAnimator != null)
                    bossAnimator.Bind(this, bossData);
            }

            Debug.Log($"[Boss] 클라이언트 초기화 — HP:{MaxHP}");
        }

        private void Update()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (!IsAlive) return;

            var state = GameManager.Instance?.CurrentState;
            if (state != GameManager.GameState.BossFight) return;

            // 타겟 갱신
            currentTarget = FindClosestAlivePlayer();

            // 이동
            if (currentTarget != null)
            {
                Vector2 dir = (currentTarget.position - transform.position).normalized;
                transform.position += (Vector3)(dir * currentMoveSpeed * Time.deltaTime);
            }

            // 접촉 데미지 타이머
            if (contactTimer > 0f)
                contactTimer -= Time.deltaTime;

            // Phase 7: HP 동기화 쓰로틀링 (호스트만)
            if (PhotonNetwork.IsMasterClient && hpDirty)
            {
                hpSyncTimer -= Time.deltaTime;
                if (hpSyncTimer <= 0f)
                    FlushHPSync();
            }
        }

        // ===== 데미지 =====

        public void TakeDamage(int damage) => TakeDamage(damage, false);

        /// <summary>
        /// 데미지 적용 + 호스트 측 비주얼. 호스트만 동작.
        /// 클라 측 비주얼은 RPC_SyncHP delta 로 표시 — Phase A 범위에선 클라 화면 isCrit 표시 미동기화.
        /// </summary>
        public void TakeDamage(int damage, bool isCrit)
        {
            if (!IsAlive) return;
            if (!PhotonNetwork.IsMasterClient) return;

            CurrentHP = Mathf.Max(0, CurrentHP - damage);

            DamagePopup.Spawn(transform.position, damage, isCrit);
            HitEffect.Spawn(transform.position);

            // 페이즈 전환 체크
            var phaseService = new Application.BossPhaseService();
            BossPhase newPhase = phaseService.DeterminePhase(
                CurrentHP, MaxHP, bossData.phase2Threshold, bossData.phase3Threshold);

            bool phaseChanged = newPhase != CurrentPhase;

            // 사망 또는 페이즈 전환 시 즉시 동기화
            if (!IsAlive || phaseChanged)
            {
                hpDirty = false;
                photonView.RPC(nameof(RPC_SyncHP), RpcTarget.All,
                    CurrentHP, MaxHP, (int)newPhase, phaseChanged);

                if (!IsAlive)
                    photonView.RPC(nameof(RPC_BossDied), RpcTarget.All);
                return;
            }

            // 일반 데미지: 쓰로틀링 (0.1초 간격 배치)
            hpDirty = true;
            if (hpSyncTimer <= 0f)
                hpSyncTimer = hpSyncInterval;
        }

        /// <summary>
        /// 클라이언트에서 호출. 보스에게 데미지 요청.
        /// Boss는 PhotonView가 있으므로 직접 RPC 전송 가능.
        /// isCrit 은 클라 측에서 굴린 결과를 그대로 호스트가 적용 (호스트 화면 색상 일치).
        /// </summary>
        public void RequestDamageFromClient(int damage, bool isCrit = false)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                TakeDamage(damage, isCrit);
                return;
            }
            photonView.RPC(nameof(RPC_RequestBossDamage), RpcTarget.MasterClient, damage, isCrit);
        }

        [PunRPC]
        private void RPC_RequestBossDamage(int damage, bool isCrit)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            TakeDamage(damage, isCrit);
        }

        /// <summary>배치된 HP를 클라이언트에 전송.</summary>
        private void FlushHPSync()
        {
            hpDirty = false;
            hpSyncTimer = 0f;

            var phaseService = new Application.BossPhaseService();
            BossPhase phase = phaseService.DeterminePhase(
                CurrentHP, MaxHP, bossData.phase2Threshold, bossData.phase3Threshold);

            photonView.RPC(nameof(RPC_SyncHP), RpcTarget.All,
                CurrentHP, MaxHP, (int)phase, false);
        }

        [PunRPC]
        private void RPC_SyncHP(int hp, int maxHp, int phaseInt, bool phaseChanged)
        {
            // 클라이언트: HP 차이로 팝업 (호스트는 TakeDamage에서 이미 처리)
            if (!PhotonNetwork.IsMasterClient)
            {
                int delta = CurrentHP - hp;
                if (delta > 0)
                {
                    DamagePopup.Spawn(transform.position, delta);
                    HitEffect.Spawn(transform.position);
                }
            }
            
            CurrentHP = hp;
            MaxHP = maxHp;
            OnHealthChanged?.Invoke(CurrentHP, MaxHP);

            if (phaseChanged)
            {
                BossPhase newPhase = (BossPhase)phaseInt;
                CurrentPhase = newPhase;
                // 이동속도는 호스트만 사용하므로 bossData null 시 스킵
                if (bossData != null)
                    currentMoveSpeed = bossData.GetMoveSpeedForPhase(newPhase);
                OnPhaseChanged?.Invoke(newPhase);
                Debug.Log($"[Boss] 페이즈 전환 → {newPhase} (HP: {CurrentHP}/{MaxHP})");
            }
        }

        [PunRPC]
        private void RPC_BossDied()
        {
            OnDied?.Invoke();
            Debug.Log("[Boss] 보스 처치!");

            // 클리어 처리는 BossSpawner 또는 GameManager에서 OnDied 구독
        }

        // ===== 공격 애니메이션 동기화 =====

        /// <summary>
        /// BossPhaseManager(호스트) 가 공격 패턴 Execute 시점에 호출.
        /// 모든 클라가 같은 시점에 보스의 Attack 트리거를 발화하도록 RPC 송신.
        /// PhaseManager 가 직접 RPC 보내지 않는 이유: 보스가 PhotonView 를 가진 PV 주체라,
        /// 보스 자신의 PV 로 보내는 것이 ViewID 매칭 / 늦참 클라 안전성 측면에서 자연스러움.
        /// </summary>
        public void RaiseAttackAnim()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            photonView.RPC(nameof(RPC_TriggerAttack), RpcTarget.All);
        }

        [PunRPC]
        private void RPC_TriggerAttack()
        {
            if (bossAnimator != null)
                bossAnimator.TriggerAttack();
        }

        // ===== 접촉 데미지 =====

        private void OnTriggerStay2D(Collider2D other)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (!IsAlive) return;
            if (contactTimer > 0f) return;

            if (other.CompareTag("Player"))
            {
                var damageable = other.GetComponent<IDamageable>();
                if (damageable != null && damageable.IsAlive)
                {
                    int dmg = bossData.GetContactDamageForPhase(CurrentPhase);
                    damageable.TakeDamage(dmg);
                    contactTimer = contactCooldown;
                }
            }
        }

        // ===== 유틸리티 =====

        /// <summary>
        /// 가장 가까운 살아있는 플레이어 반환.
        /// </summary>
        public Transform FindClosestAlivePlayer()
        {
            var players = GameObject.FindGameObjectsWithTag("Player");
            Transform closest = null;
            float minDist = float.MaxValue;

            foreach (var p in players)
            {
                var damageable = p.GetComponent<IDamageable>();
                if (damageable == null || !damageable.IsAlive) continue;

                float dist = Vector2.Distance(transform.position, p.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = p.transform;
                }
            }

            return closest;
        }

        /// <summary>
        /// 보스 혼돈 스킬에서 이동속도를 추가 변경할 때 사용.
        /// </summary>
        public void SetMoveSpeed(float speed)
        {
            currentMoveSpeed = speed;
        }

        /// <summary>
        /// 보스 혼돈 스킬에서 HP를 변경할 때 사용 (유리대포).
        /// </summary>
        public void ApplyHPMultiplier(float multiplier)
        {
            MaxHP = Mathf.RoundToInt(MaxHP * multiplier);
            CurrentHP = Mathf.Min(CurrentHP, MaxHP);
            OnHealthChanged?.Invoke(CurrentHP, MaxHP);
        }
    }
}