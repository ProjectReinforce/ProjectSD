using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;
using SwDreams.Domain.Interfaces;
using SwDreams.Adapter.Skill;
using SwDreams.Data;
using SwDreams.Adapter.Manager;
using SwDreams.Presentation;

namespace SwDreams.Testing
{
    /// <summary>
    /// Phase 1 완료 전까지 사용하는 임시 플레이어.
    /// WASD 이동 + Photon 동기화 + IDamageable + 스킬 발동.
    /// 
    /// 프리팹 구성:
    /// - PlayerStub (이 스크립트)
    /// - PhotonView + PhotonTransformView
    /// - Rigidbody2D (Gravity 0, Freeze Rotation Z)
    /// - CircleCollider2D (isTrigger = false, Player 레이어)
    /// - SpriteRenderer
    /// - 자식 오브젝트 "SkillSlot": Skill + ProjectileEffect
    ///     → ProjectileEffect의 projectilePrefab에 Projectile 프리팹 연결
    /// 
    /// Resources 폴더에 "PlayerStub"으로 저장.
    /// Tag: "Player"
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class PlayerStub : MonoBehaviourPun, IDamageable
    {
        [Header("스탯")]
        [SerializeField] private int maxHP = 100;
        [SerializeField] private float moveSpeed = 5f;

        [Header("스킬 (테스트용)")]
        [SerializeField] private SkillData startingSkillData;

        public int CurrentHP { get; private set; }
        public int MaxHP => maxHP;
        public bool IsAlive => CurrentHP > 0;

        public event Action<int, int> OnHealthChanged;
        public event Action OnDied;
        
        /// <summary>부활 시 발생. DeathOverlayUI에서 구독.</summary>
        public event Action OnRespawned;

        // Phase 6: 사망/부활 상태
        private bool isDead = false;

        private Rigidbody2D rb;
        private SkillManager skillManager;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            CurrentHP = maxHP;
            gameObject.tag = "Player";

            skillManager = GetComponentInChildren<SkillManager>();
        }

        private void Start()
        {
            // 모든 클라이언트에서 모든 플레이어의 스킬 실행.
            // 투사체는 로컬 렌더링, 데미지는 호스트만 처리.
            if (startingSkillData != null && skillManager != null)
            {
                skillManager.AcquireSkill(startingSkillData);
                Debug.Log($"[PlayerStub] 시작 스킬 획득: {startingSkillData.skillName}");
            }
            else
            {
                Debug.LogWarning("[PlayerStub] startingSkillData 또는 SkillManager 없음");
            }

            // 패시브 테스트 (임시)
            // var testStats = GetComponent<PlayerStats>();
            // if (testStats != null)
            // {
            //     testStats.RecalculateAll();
            //     Debug.Log($"[Test] MoveSpeed: {testStats.MoveSpeed}, ATK: {testStats.AttackMultiplier}");
            // }

            // 로컬 플레이어만 LevelUpManager에 등록
            if (photonView.IsMine && LevelUpManager.Instance != null)
            {
                LevelUpManager.Instance.RegisterLocalPlayer(skillManager);
            }
            
            // Phase 6: RespawnManager에 등록
            if (PhotonNetwork.IsMasterClient && RespawnManager.Instance != null)
                RespawnManager.Instance.RegisterPlayer(photonView.ViewID);
        }

        private void Update()
        {
            if (!photonView.IsMine) return;

            if (isDead)
            {
                rb.linearVelocity = Vector2.zero;
                return;
            }

            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentState != GameManager.GameState.Playing &&
                GameManager.Instance.CurrentState != GameManager.GameState.BossFight)
            {
                rb.linearVelocity = Vector2.zero;
                return;
            }

            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            Vector2 input = Vector2.zero;
            if (kb.wKey.isPressed) input.y += 1f;
            if (kb.sKey.isPressed) input.y -= 1f;
            if (kb.dKey.isPressed) input.x += 1f;
            if (kb.aKey.isPressed) input.x -= 1f;
            input = input.normalized;

            rb.linearVelocity = input * moveSpeed;

            if (PhotonNetwork.IsMasterClient && GameManager.Instance != null)
            {
                if (kb.lKey.wasPressedThisFrame)
                {
                    GameManager.Instance?.AddExp(100);
                    Debug.Log("[Test] 강제 레벨업");
                }

                // K: 게임 강제 재개 (Paused → Playing)
                if (kb.kKey.wasPressedThisFrame)
                {
                    GameManager.Instance?.ChangeStateNetwork(GameManager.GameState.Playing);
                    UIManager.Instance?.HideLevelUp();
                    Debug.Log("[Test] 강제 재개");
                }

                // P: 스탯 확인
                if (kb.pKey.wasPressedThisFrame)
                {
                    var stats = GetComponent<PlayerStats>();
                    if (stats != null)
                    {
                        Debug.Log($"[Stats] ATK: {stats.AttackMultiplier:F2}, " +
                                $"MoveSpeed: {stats.MoveSpeed:F1}, " +
                                $"ProjSpeed: {stats.ProjectileSpeedBonus:F1}, " +
                                $"ProjCount: {stats.ProjectileCountBonus}");
                    }
                    
                    if (skillManager != null)
                        skillManager.LogSlotStatus();
                }

                // T: 플레이어 즉사 (사망/부활 테스트)
                if (kb.tKey.wasPressedThisFrame)
                {
                    TakeDamage(9999);
                    Debug.Log("[Test] 강제 즉사");
                }
                
                // B: 보스 즉시 소환
                if (kb.bKey.wasPressedThisFrame)
                {
                    BossSpawner.Instance?.DebugSpawnBoss();
                    Debug.Log("[Test] 보스 강제 소환");
                }

                // H: 보스 HP 30%로 설정 (Phase 3 테스트)
                if (kb.hKey.wasPressedThisFrame)
                {
                    var boss = BossSpawner.Instance?.CurrentBoss;
                    if (boss != null && boss.IsAlive)
                    {
                        int targetHP = Mathf.RoundToInt(boss.MaxHP * 0.3f);
                        int dmg = boss.CurrentHP - targetHP;
                        if (dmg > 0) boss.TakeDamage(dmg);
                        Debug.Log($"[Test] 보스 HP → 30% ({targetHP})");
                    }
                }
            }
        }

        /// <summary>
        /// 호스트에서 호출. 해당 플레이어에게 RPC로 데미지 전달.
        /// </summary>
        public void TakeDamage(int damage)
        {
            if (!IsAlive) return;

            // 호스트가 판정 → 해당 플레이어의 모든 클라이언트에 동기화
            photonView.RPC(nameof(RPC_TakeDamage), RpcTarget.All, damage);
        }

        [PunRPC]
        private void RPC_TakeDamage(int damage)
        {
            if (!IsAlive) return;

            CurrentHP = Mathf.Clamp(CurrentHP - damage, 0, MaxHP);
            OnHealthChanged?.Invoke(CurrentHP, MaxHP);
            Debug.Log($"[PlayerStub] HP: {CurrentHP}/{MaxHP} (dmg:{damage})");

            if (!IsAlive && !isDead)
            {
                isDead = true;
                OnDied?.Invoke();
                SetDeadVisual(true);
                Debug.Log("[PlayerStub] 사망!");

                // 호스트: RespawnManager에 부활 요청
                if (PhotonNetwork.IsMasterClient && RespawnManager.Instance != null)
                    RespawnManager.Instance.RequestRespawn(photonView.ViewID);
            }
        }

        // ===== Phase 6: 사망/부활 =====

        /// <summary>
        /// RespawnManager의 RPC에서 호출. 모든 클라이언트에서 실행.
        /// </summary>
        public void LocalRespawn(int newHP)
        {
            CurrentHP = Mathf.Clamp(newHP, 1, MaxHP);
            isDead = false;
            SetDeadVisual(false);
            OnHealthChanged?.Invoke(CurrentHP, MaxHP);
            OnRespawned?.Invoke();
            Debug.Log($"[PlayerStub] 부활! HP: {CurrentHP}/{MaxHP}");
        }

        private void SetDeadVisual(bool dead)
        {
            // 반투명 처리
            var sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                var c = sr.color;
                c.a = dead ? 0.3f : 1f;
                sr.color = c;
            }

            // 스킬 일시정지/재개
            if (skillManager != null)
            {
                if (dead) skillManager.PauseAllSkills();
                else skillManager.ResumeAllSkills();
            }
        }
    }
}
