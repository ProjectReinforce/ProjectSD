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

        // Phase 7: 슬로우 상태
        private float slowMultiplier = 1f;

        // Phase 7: 캐릭터 데이터 연동
        private bool isInitialized = false;
        private CharacterData characterData;

        private Rigidbody2D rb;
        private SkillManager skillManager;

        /// <summary>
        /// PhotonView.InstantiationData에서 characterId를 읽어 자동 초기화.
        /// GamePlayerSpawner 경유 시: InstantiationData[0] = characterId (int).
        /// TestPlayerSpawner 경유 시: InstantiationData가 null → 아무 일도 안 함.
        /// </summary>
        private void TryInitializeFromInstantiationData()
        {
            if (photonView.InstantiationData == null || photonView.InstantiationData.Length == 0)
                return;

            int characterId = (int)photonView.InstantiationData[0];
            var db = GameManager.Instance?.CharacterDB;
            if (db == null)
            {
                Debug.LogWarning("[PlayerStub] CharacterDatabase를 찾을 수 없습니다. " +
                                 "GameManager에 CharacterDatabase SO를 연결하세요.");
                return;
            }

            var data = db.GetById(characterId);
            if (data == null)
            {
                Debug.LogWarning($"[PlayerStub] CharacterData ID {characterId}를 찾을 수 없습니다. " +
                                 "기본 캐릭터로 폴백합니다.");
                data = db.GetDefault();
            }

            if (data != null)
                Initialize(data);
        }

        /// <summary>
        /// 정상 플로우: GamePlayerSpawner가 스폰 후 Start() 이전에 호출.
        /// CharacterData 기반으로 스탯/시작스킬/고유특성 초기화.
        /// 
        /// 테스트 모드: 호출되지 않으면 기존 인스펙터 값(startingSkillData, maxHP 등) 사용.
        /// </summary>
        public void Initialize(CharacterData data)
        {
            if (data == null)
            {
                Debug.LogWarning("[PlayerStub] Initialize: CharacterData가 null입니다.");
                return;
            }

            characterData = data;
            isInitialized = true;

            // 캐릭터 base 스탯 적용
            maxHP = data.maxHP;
            moveSpeed = data.moveSpeed;
            CurrentHP = maxHP;

            // PlayerStats에 캐릭터 base 스탯 전달
            var stats = GetComponent<PlayerStats>();
            if (stats != null)
                stats.ApplyCharacterBase(data);

            Debug.Log($"[PlayerStub] 캐릭터 초기화: {data.displayName} " +
                      $"(HP:{maxHP}, Speed:{moveSpeed})");
        }

        /// <summary>현재 캐릭터 데이터. 결과 화면에서 빌드 요약 시 사용.</summary>
        public CharacterData CharacterData => characterData;

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
            // Phase 7: InstantiationData에서 캐릭터 ID 자동 감지 (정상 플로우)
            // GamePlayerSpawner가 instantiationData로 characterId를 넘김.
            // 테스트 모드(TestPlayerSpawner)에서는 InstantiationData가 null → 스킵.
            if (!isInitialized)
                TryInitializeFromInstantiationData();

            // 시작 스킬 결정: Initialize() 호출 여부에 따라 분기
            SkillData activeToAcquire = null;
            SkillData passiveToAcquire = null;

            if (isInitialized && characterData != null)
            {
                // 정상 플로우: CharacterData의 시작 액티브 + 패시브
                activeToAcquire = characterData.startingActiveSkill;
                passiveToAcquire = characterData.startingPassiveSkill;
            }
            else
            {
                // 테스트 모드: 인스펙터에 설정된 스킬 (액티브만)
                activeToAcquire = startingSkillData;
            }

            if (skillManager != null)
            {
                if (activeToAcquire != null)
                {
                    skillManager.AcquireSkill(activeToAcquire);
                    Debug.Log($"[PlayerStub] 시작 액티브 획득: {activeToAcquire.skillName}" +
                              $"{(isInitialized ? " (캐릭터)" : " (테스트)")}");
                }

                if (passiveToAcquire != null)
                {
                    skillManager.AcquireSkill(passiveToAcquire);
                    Debug.Log($"[PlayerStub] 시작 패시브 획득: {passiveToAcquire.skillName}");
                }

                if (activeToAcquire == null)
                    Debug.LogWarning("[PlayerStub] 시작 액티브 스킬 없음");
            }
            else
            {
                Debug.LogWarning("[PlayerStub] SkillManager 없음");
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

            rb.linearVelocity = input * moveSpeed * slowMultiplier;

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

        // ===== Phase 7: 슬로우 =====

        /// <summary>
        /// 이동속도에 배율 적용. BossPhaseManager.RPC_ApplyGlobalSlow에서 호출.
        /// multiplier = 0.5 → 이동속도 50%
        /// </summary>
        public void ApplySlow(float multiplier)
        {
            slowMultiplier = Mathf.Clamp01(multiplier);
            Debug.Log($"[PlayerStub] 슬로우 적용: {multiplier * 100}%");
        }

        public void RemoveSlow()
        {
            slowMultiplier = 1f;
            Debug.Log("[PlayerStub] 슬로우 해제");
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
