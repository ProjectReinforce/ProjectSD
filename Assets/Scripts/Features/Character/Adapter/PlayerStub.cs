using System;
using SwDreams.Features.Progression.Adapter;
using SwDreams.Features.Character.Domain.ValueObjects;
using SwDreams.Features.Character.Adapter.Data;
using SwDreams.Features.Character.Adapter;
using SwDreams.Features.Skill.Adapter.Data;
using UnityEngine;
using Photon.Pun;
using SwDreams.Shared.Domain.Interfaces;
using SwDreams.Features.Skill.Adapter;
using SwDreams.Shared.Data;
using SwDreams.Shared.Managers;
using SwDreams.Features.Pickup.Adapter;

namespace SwDreams.Features.Character.Adapter
{
    /// <summary>
    /// 플레이어 오케스트레이터. 각 컴포넌트를 연결하고 외부 API를 유지.
    ///
    /// [Phase 7 리팩토링] Step 2-6: 책임 분리 완료.
    /// - PlayerHealth: HP, 데미지, 사망/부활
    /// - PlayerMovement: 이동, 슬로우
    /// - PlayerVisual: 피격 플래시, 사망 반투명
    /// - DebugInputHandler: 디버그 키 (에디터 전용)
    /// - PlayerStats: 스탯 관리 (StatModifier 기반)
    ///
    /// 이 클래스는 IDamageable을 유지하여 기존 외부 코드와 호환.
    /// 
    /// 프리팹 구성:
    /// - PlayerStub + PlayerHealth + PlayerMovement + PlayerVisual + PlayerStats
    /// - PhotonView + PhotonTransformView + Rigidbody2D + CircleCollider2D
    /// - 자식: SkillManager + ChaosSkillManager
    /// - (에디터) DebugInputHandler
    /// Tag: "Player"
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    [RequireComponent(typeof(PlayerHealth))]
    [RequireComponent(typeof(PlayerMovement))]
    [RequireComponent(typeof(PlayerVisual))]
    public class PlayerStub : MonoBehaviourPun, IDamageable
    {
        [Header("스킬 (테스트용)")]
        [SerializeField] private SkillData startingSkillData;

        // ===== 컴포넌트 참조 =====
        private PlayerHealth playerHealth;
        private PlayerMovement playerMovement;
        private PlayerVisual playerVisual;
        private SkillManager skillManager;

        // ===== 초기화 상태 =====
        private bool isInitialized = false;
        private CharacterData characterData;

        // ===== IDamageable (PlayerHealth 위임) =====
        public int CurrentHP => playerHealth != null ? playerHealth.CurrentHP : 0;
        public int MaxHP => playerHealth != null ? playerHealth.MaxHP : 0;
        public bool IsAlive => playerHealth != null && playerHealth.IsAlive;

        public event Action<int, int> OnHealthChanged;
        public event Action OnDied;
        public event Action OnRespawned;

        /// <summary>현재 캐릭터 데이터. 결과 화면에서 빌드 요약 시 사용.</summary>
        public CharacterData CharacterData => characterData;

        // ===== 초기화 =====

        private void Awake()
        {
            playerHealth = GetComponent<PlayerHealth>();
            playerMovement = GetComponent<PlayerMovement>();
            playerVisual = GetComponent<PlayerVisual>();
            skillManager = GetComponentInChildren<SkillManager>();
            gameObject.tag = "Player";
        }

        private void Start()
        {
            // 컴포넌트 바인딩
            var stats = GetComponent<PlayerStats>();
            playerHealth.BindToStats(stats);
            playerMovement.Bind(playerHealth, stats);
            playerVisual.Bind(playerHealth);

            // PlayerHealth 이벤트 → PlayerStub 이벤트 전달 (외부 호환)
            playerHealth.OnHealthChanged += (cur, max) => OnHealthChanged?.Invoke(cur, max);
            playerHealth.OnDied += () => OnDied?.Invoke();
            playerHealth.OnRespawned += () => OnRespawned?.Invoke();

            // 캐릭터 초기화 (정상 플로우: InstantiationData)
            if (!isInitialized)
                TryInitializeFromInstantiationData();

            // 시작 스킬 획득
            AcquireStartingSkills();

            // 로컬 플레이어만 LevelUpManager에 등록
            if (photonView.IsMine && LevelUpManager.Instance != null)
                LevelUpManager.Instance.RegisterLocalPlayer(skillManager);

            // RespawnManager에 등록
            if (PhotonNetwork.IsMasterClient && RespawnManager.Instance != null)
                RespawnManager.Instance.RegisterPlayer(photonView.ViewID);
        }

        // ===== IDamageable 위임 =====

        public void TakeDamage(int damage)
        {
            playerHealth?.ApplyDamage(damage);
        }

        // ===== 부활 (RespawnManager에서 호출) =====

        public void LocalRespawn(int newHP)
        {
            playerHealth?.Respawn(newHP);
        }

        // ===== 슬로우 (BossPhaseManager에서 호출) =====

        public void ApplySlow(float multiplier)
        {
            playerMovement?.ApplySlow(multiplier);
        }

        public void RemoveSlow()
        {
            playerMovement?.RemoveSlow();
        }

        // ===== 픽업 위임 (PickupItemBase 가 호출) =====

        /// <summary>
        /// 클라가 픽업 키 누른 시점에 호스트로 픽업 처리 위임.
        /// PickupItemBase 자체엔 PhotonView 가 없어서 Player 메인 PV 의 RPC 사용.
        /// 호스트는 자기 측 같은 itemId 의 가장 가까운 픽업 인스턴스를 찾아 ProcessPickupAsHost.
        /// </summary>
        public void RequestPickupFromClient(Vector3 worldPos, string itemId)
        {
            photonView.RPC(nameof(RPC_HostPickup), RpcTarget.MasterClient,
                worldPos.x, worldPos.y, itemId);
        }

        // ===== [N15/N17] 스킬 발사 동기화 RPC (Phase 1) =====
        // Client-decided + Host-trusted 패턴.
        // 자기 클라가 spawnPos / spawnDir 결정 + RPC 송신:
        //   - 자기 = 호스트 → RPC_BroadcastSkillSpawn (Others)
        //   - 자기 ≠ 호스트 → RPC_RequestSkillSpawn (MasterClient)
        // 호스트가 RequestSpawn 수신 시 자기 측 Spawn 처리(데미지 권위) + Others 에 Broadcast.
        // 다른 클라가 Broadcast 수신 시 자기 측 Spawn (시각만).

        [PunRPC]
        private void RPC_RequestSkillSpawn(int skillId, Vector2 baseDir, Vector2 spawnPos, bool hasSpawnPosOverride, int fireIndex, int totalCount)
        {
            if (!PhotonNetwork.IsMasterClient) return;

            // 호스트 자기 측 Spawn (데미지 권위)
            DispatchNetworkSkillSpawn(skillId, baseDir, spawnPos, hasSpawnPosOverride, fireIndex, totalCount);

            // 다른 클라들에 broadcast (자기 = master 송신자 제외)
            photonView.RPC(nameof(RPC_BroadcastSkillSpawn), RpcTarget.Others,
                skillId, baseDir, spawnPos, hasSpawnPosOverride, fireIndex, totalCount);
        }

        [PunRPC]
        private void RPC_BroadcastSkillSpawn(int skillId, Vector2 baseDir, Vector2 spawnPos, bool hasSpawnPosOverride, int fireIndex, int totalCount)
        {
            // 자기 PhotonView (자기 클라가 본인) 면 이미 prediction 했으므로 무시.
            if (photonView.IsMine) return;
            DispatchNetworkSkillSpawn(skillId, baseDir, spawnPos, hasSpawnPosOverride, fireIndex, totalCount);
        }

        private void DispatchNetworkSkillSpawn(int skillId, Vector2 baseDir, Vector2 spawnPos, bool hasSpawnPosOverride, int fireIndex, int totalCount)
        {
            if (skillManager == null) return;
            skillManager.HandleNetworkSkillSpawn(skillId, baseDir, spawnPos, hasSpawnPosOverride, fireIndex, totalCount);
        }

        [PunRPC]
        private void RPC_HostPickup(float x, float y, string itemId)
        {
            if (!PhotonNetwork.IsMasterClient) return;

            // 호스트 측에서 같은 itemId 의 가장 가까운 활성 PickupItemBase 인스턴스 찾기.
            // 1m 이내 매칭 — 같은 itemId 가 동시에 매우 가까이 있는 경우는 거의 없음.
            const float MAX_MATCH_SQR = 1f;
            var pickups = FindObjectsByType<PickupItemBase>(FindObjectsSortMode.None);
            PickupItemBase target = null;
            float minSqr = MAX_MATCH_SQR;
            Vector2 reqPos = new Vector2(x, y);

            for (int i = 0; i < pickups.Length; i++)
            {
                var p = pickups[i];
                if (p == null || !p.gameObject.activeInHierarchy) continue;
                if (p.ItemId != itemId) continue;
                float d = Vector2.SqrMagnitude((Vector2)p.transform.position - reqPos);
                if (d < minSqr)
                {
                    minSqr = d;
                    target = p;
                }
            }

            if (target == null)
            {
                Debug.LogWarning($"[PlayerStub] RPC_HostPickup itemId={itemId} pos={reqPos} — 매칭 인스턴스 없음.");
                return;
            }

            target.ProcessPickupAsHost(gameObject);
        }

        // ===== 캐릭터 초기화 =====

        private void TryInitializeFromInstantiationData()
        {
            if (photonView.InstantiationData == null || photonView.InstantiationData.Length == 0)
                return;

            int characterId = (int)photonView.InstantiationData[0];
            var db = GameManager.Instance?.CharacterDB;
            if (db == null)
            {
                Debug.LogWarning("[PlayerStub] CharacterDatabase를 찾을 수 없습니다.");
                return;
            }

            var data = db.GetById(characterId);
            if (data == null)
            {
                Debug.LogWarning($"[PlayerStub] CharacterData ID {characterId}를 찾을 수 없습니다.");
                data = db.GetDefault();
            }

            if (data != null)
                Initialize(data);
        }

        /// <summary>
        /// CharacterData 기반 초기화. GamePlayerSpawner에서 호출.
        /// </summary>
        public void Initialize(CharacterData data)
        {
            if (data == null) return;

            characterData = data;
            isInitialized = true;

            // 각 컴포넌트에 base 스탯 전달
            playerHealth.SetMaxHP(data.maxHP);
            playerMovement.SetMoveSpeed(data.moveSpeed);

            var stats = GetComponent<PlayerStats>();
            if (stats != null)
                stats.ApplyCharacterBase(data);

            Debug.Log($"[PlayerStub] 캐릭터 초기화: {data.displayName} (HP:{data.maxHP}, Speed:{data.moveSpeed})");
        }

        // ===== 시작 스킬 =====

        private void AcquireStartingSkills()
        {
            if (skillManager == null) return;

            SkillData activeToAcquire = null;
            SkillData passiveToAcquire = null;

            if (isInitialized && characterData != null)
            {
                activeToAcquire = characterData.startingActiveSkill;
                passiveToAcquire = characterData.startingPassiveSkill;
            }
            else
            {
                activeToAcquire = startingSkillData;
            }

            if (activeToAcquire != null)
            {
                skillManager.AcquireSkill(activeToAcquire);
                Debug.Log($"[PlayerStub] 시작 액티브: {activeToAcquire.skillName}" +
                          $"{(isInitialized ? " (캐릭터)" : " (테스트)")}");
            }

            if (passiveToAcquire != null)
            {
                skillManager.AcquireSkill(passiveToAcquire);
                Debug.Log($"[PlayerStub] 시작 패시브: {passiveToAcquire.skillName}");
            }

            if (activeToAcquire == null)
                Debug.LogWarning("[PlayerStub] 시작 액티브 스킬 없음");
        }
    }
}