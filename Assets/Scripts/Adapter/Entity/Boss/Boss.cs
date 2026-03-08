using System;
using UnityEngine;
using Photon.Pun;
using SwDreams.Domain;
using SwDreams.Domain.Interfaces;
using SwDreams.Data;
using SwDreams.Adapter.Manager;

namespace SwDreams.Adapter.Entity
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
    public class Boss : MonoBehaviourPun, IDamageable
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

        // 컴포넌트 캐시
        private SpriteRenderer spriteRenderer;

        private void Awake()
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
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

            Debug.Log($"[Boss] 초기화 — {data.bossName}, HP:{MaxHP} ({playerCount}인 스케일링)");
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
        }

        // ===== 데미지 =====

        public void TakeDamage(int damage)
        {
            if (!IsAlive) return;
            if (!PhotonNetwork.IsMasterClient) return;

            CurrentHP = Mathf.Max(0, CurrentHP - damage);

            // 페이즈 전환 체크
            var phaseService = new Application.BossPhaseService();
            BossPhase newPhase = phaseService.DeterminePhase(
                CurrentHP, MaxHP, bossData.phase2Threshold, bossData.phase3Threshold);

            bool phaseChanged = newPhase != CurrentPhase;

            // 모든 클라이언트에 동기화
            photonView.RPC(nameof(RPC_SyncHP), RpcTarget.All,
                CurrentHP, MaxHP, (int)newPhase, phaseChanged);

            if (!IsAlive)
            {
                photonView.RPC(nameof(RPC_BossDied), RpcTarget.All);
            }
        }

        [PunRPC]
        private void RPC_SyncHP(int hp, int maxHp, int phaseInt, bool phaseChanged)
        {
            CurrentHP = hp;
            MaxHP = maxHp;
            OnHealthChanged?.Invoke(CurrentHP, MaxHP);

            if (phaseChanged)
            {
                BossPhase newPhase = (BossPhase)phaseInt;
                CurrentPhase = newPhase;
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