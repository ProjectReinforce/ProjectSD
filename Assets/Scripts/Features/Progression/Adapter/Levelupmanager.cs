using System.Collections.Generic;
using SwDreams.Features.UI.Presentation;
using SwDreams.Features.Progression.Application;
using SwDreams.Features.Progression.Adapter;
using SwDreams.Features.Progression.Domain;
using SwDreams.Features.Character.Adapter;
using SwDreams.Features.Boss.Adapter;
using SwDreams.Features.Skill.Adapter.Data;
using SwDreams.Features.Skill.Adapter.Chaos;
using SwDreams.Features.StatBoost.Adapter;
using SwDreams.Features.StatBoost.Adapter.Data;
using SwDreams.Shared.Domain.ValueObjects;
using SwDreams.Shared.Managers;
using UnityEngine;
using Photon.Pun;
using SwDreams.Shared.Data;
using SwDreams.Features.Skill.Adapter;

namespace SwDreams.Features.Progression.Adapter
{
    /// <summary>
    /// 레벨업 네트워크 오케스트레이터.
    /// 레벨업 발생 → 일시정지 → 선택지 전송 → 선택 수집 → 게임 재개.
    ///
    /// 호스트 권한:
    /// - 선택지 생성 (각 플레이어의 SkillManager 상태 참조)
    /// - 타임아웃 관리
    /// - 전원 선택 완료 판정 → 게임 재개
    ///
    /// 클라이언트:
    /// - 선택지 수신 → UI 표시 (LevelUpPanel)
    /// - 선택 결과를 호스트에 반환
    ///
    /// 셋업:
    /// GameScene에 빈 GameObject "LevelUpManager"
    /// → LevelUpManager + PhotonView 부착
    /// → skillDatabase 인스펙터에서 SkillDatabase SO 연결
    ///
    /// 의존:
    /// - GameManager (상태 전환, OnLevelUp 이벤트)
    /// - SkillManager (각 플레이어의 스킬 슬롯)
    /// - SkillDatabase (전체 스킬 풀)
    /// - ExperienceService (혼돈 스킬 레벨 판정)
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class LevelUpManager : MonoBehaviourPun
    {
        public static LevelUpManager Instance { get; private set; }

        [Header("데이터")]
        [SerializeField] private SkillDatabase skillDatabase;

        [Header("설정")]
        [SerializeField] private float selectionTimeout = 15f;

        // ===== 상태 =====
        private bool isLevelUpActive = false;
        private float timeoutTimer;
        
        // Phase 6: 현재 처리 중인 레벨 (보스 혼돈 스킬 결정용)
        private int currentProcessingLevel;

        // Phase 6: 레벨업 진입 전 상태 저장 (보스전 중 레벨업 대응)
        private GameManager.GameState stateBeforeLevelUp;

        // 레벨업 큐: 진행 중일 때 추가 레벨업이 발생하면 대기열에 저장
        private Queue<int> pendingLevelUps = new Queue<int>();

        // Phase 6 (F2): 퀘스트 보상 큐. 레벨업 진행 중 RequestQuestReward 호출 시
        // 보상이 손실되지 않도록 적재 후 EndLevelUpSequence 에서 처리.
        private Queue<float[]> pendingQuestRewards = new Queue<float[]>();

        // 호스트 전용: 각 플레이어의 선택 완료 여부
        // key = Photon ActorNumber, value = 선택 완료 여부
        private Dictionary<int, bool> playerSelections = new Dictionary<int, bool>();

        // 호스트 전용: 각 플레이어에게 보낸 선택지 (타임아웃 시 랜덤 선택용)
        // key = ActorNumber, value = 선택지 ID 배열 (스킬 ID 또는 boost ID)
        private Dictionary<int, int[]> playerChoices = new Dictionary<int, int[]>();

        // 호스트 전용: 각 플레이어에게 어떤 kind 의 선택지를 보냈는지 추적.
        // 타임아웃 랜덤 처리 시 ApplyChoice(skill) vs StatBoostManager.ApplyChoice(boost) 분기.
        private Dictionary<int, ChoicePanelKind> playerPanelKinds = new Dictionary<int, ChoicePanelKind>();

        // 호스트 전용: StatBoost 패널일 때 각 플레이어에게 전달된 rolledRarity.
        // ApplyBoostLocally 에서 value 해석 시 사용.
        private Dictionary<int, Rarity> playerRolledRarities = new Dictionary<int, Rarity>();

        // 클라이언트: 내가 받은 선택지
        private SkillData[] myChoices;

        // 클라이언트: 내가 받은 StatBoost 선택지 (kind == StatBoost 일 때만)
        private StatBoostData[] myBoostChoices;

        // 클라이언트: StatBoost 패널에 함께 수신한 rolledRarity (apply + 카드 표시용).
        private Rarity myBoostRarity = Rarity.Common;

        // 클라이언트: 혼돈 패널에 함께 수신한 rolledRarity (SubmitChoice 시 함께 전송).
        private Rarity myChaosRarity = Rarity.Common;

        // // ===== 이벤트 (UI 연결용) =====
        // /// <summary>선택지 수신 시 발생. LevelUpPanel이 구독.</summary>
        // public event System.Action<SkillData[]> OnChoicesReceived;

        // /// <summary>타이머 갱신. UI 타이머 바 용.</summary>
        // public event System.Action<float, float> OnTimerUpdated; // remaining, total

        // /// <summary>레벨업 종료 (게임 재개). UI 닫기용.</summary>
        // public event System.Action OnLevelUpEnded;

        // /// <summary>혼돈 스킬 선택지 수신 시 발생.</summary>
        // public event System.Action<SkillData[]> OnChaosChoicesReceived;

        // ===== 로컬 플레이어 참조 =====
        // PlayerStub이 스폰된 후 등록해야 함
        private SkillManager localSkillManager;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnLevelUp -= OnTeamLevelUp;
                GameManager.Instance.OnLevelUp += OnTeamLevelUp;
                Debug.Log("[LevelUpManager] GameManager.OnLevelUp 구독 완료");
            }
            else
            {
                Debug.LogError("[LevelUpManager] Start() 시점에 GameManager 없음!");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnLevelUp += OnTeamLevelUp;
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnLevelUp -= OnTeamLevelUp;
        }

        /// <summary>
        /// 로컬 플레이어 스폰 후 호출.
        /// PlayerStub.Start() 또는 Player.Start()에서 호출해야 함.
        /// </summary>
        public void RegisterLocalPlayer(SkillManager sm)
        {
            localSkillManager = sm;
            Debug.Log("[LevelUpManager] 로컬 플레이어 등록 완료");
        }

        // ===== 레벨업 감지 =====

        /// <summary>
        /// GameManager.OnLevelUp 이벤트 핸들러.
        /// 모든 클라이언트에서 호출되지만, 실제 처리는 호스트만.
        /// </summary>
        private void OnTeamLevelUp(int newLevel)
        {
            if (!PhotonNetwork.IsMasterClient) return;

            // 이미 레벨업 진행 중이면 큐에 저장
            if (isLevelUpActive)
            {
                pendingLevelUps.Enqueue(newLevel);
                Debug.Log($"[LevelUpManager] 레벨업 대기열 추가: Lv.{newLevel} (대기: {pendingLevelUps.Count}개)");
                return;
            }

            Debug.Log($"[LevelUpManager] 팀 레벨업! Lv.{newLevel}");

            currentProcessingLevel = newLevel;
            bool isChaosLevel = GameManager.Instance?.Config != null
                && GameManager.Instance.Config.IsChaosLevel(newLevel);
            StartLevelUpSequence(isChaosLevel);
        }

        // ===== 호스트: 레벨업 시퀀스 시작 =====

        private void StartLevelUpSequence(bool isChaosLevel)
        {
            isLevelUpActive = true;
            timeoutTimer = selectionTimeout;
            playerSelections.Clear();
            playerChoices.Clear();
            playerPanelKinds.Clear();
            playerRolledRarities.Clear();

            // 현재 상태 저장 (Playing 또는 BossFight)
            if (GameManager.Instance.CurrentState != GameManager.GameState.Paused)
                stateBeforeLevelUp = GameManager.Instance.CurrentState;
            // 게임 일시정지
            GameManager.Instance.ChangeStateNetwork(GameManager.GameState.Paused);

            StartLevelUpSequenceInternal(isChaosLevel);
        }

        /// <summary>
        /// 선택지 생성 + 전송. Paused 전환 없이 호출 가능 (대기열 처리용).
        /// </summary>
        private void StartLevelUpSequenceInternal(bool isChaosLevel)
        {
            isLevelUpActive = true;
            timeoutTimer = selectionTimeout;

            foreach (var player in PhotonNetwork.PlayerList)
            {
                playerSelections[player.ActorNumber] = false;

                if (isChaosLevel)
                    SendChaosChoices(player);
                else
                    SendNormalChoices(player);
            }

            photonView.RPC(nameof(RPC_StartTimer), RpcTarget.All, selectionTimeout);
        }

        // ===== 호스트: 선택지 생성 + 전송 =====

        private void SendNormalChoices(Photon.Realtime.Player player)
        {
            // 해당 플레이어의 SkillManager 찾기
            SkillManager sm = FindSkillManagerForPlayer(player.ActorNumber);
            if (sm == null)
            {
                Debug.LogWarning($"[LevelUpManager] 플레이어 {player.ActorNumber}의 SkillManager 찾기 실패");
                // SkillManager 없으면 선택 완료 처리 (스킵)
                playerSelections[player.ActorNumber] = true;
                return;
            }

            // 선택지 생성
            SkillData[] normalPool = skillDatabase.GetNormalPool();
            SkillData[] choices = sm.GenerateChoices(normalPool, 3);

            // "만렙" 정의: 스킬 풀 고갈 (더 이상 등장할 스킬 없음) → StatBoost 선택지로 대체.
            if (choices.Length == 0)
            {
                Debug.Log($"[LevelUpManager] Player {player.ActorNumber} 스킬 풀 고갈 — StatBoost 분기로 전환.");
                SendStatBoostChoices(player);
                return;
            }

            // 스킬 ID 배열로 변환 (RPC는 SO를 직접 못 보내므로)
            int[] choiceIds = new int[choices.Length];
            for (int i = 0; i < choices.Length; i++)
                choiceIds[i] = choices[i].skillId;

            // 저장 (타임아웃 시 랜덤 선택용)
            playerChoices[player.ActorNumber] = choiceIds;
            playerPanelKinds[player.ActorNumber] = ChoicePanelKind.Skill;

            Debug.Log($"[LevelUpManager] → Player {player.ActorNumber} 선택지: " +
                    string.Join(", ", System.Array.ConvertAll(choices, s => s.skillName)));

            // 해당 플레이어에게만 RPC 전송 (Skill 은 등급 무관 → rarityInt=0)
            photonView.RPC(nameof(RPC_ReceiveChoices), player,
                choiceIds, (int)ChoicePanelKind.Skill, 0);
        }

        /// <summary>
        /// 능력치 부스트 선택지 생성 + 전송. SendNormalChoices 의 "만렙" 분기.
        /// Phase 6 퀘스트 보상에서도 동일 경로로 트리거 가능.
        ///
        /// 통합 등급 SO 방식: SO 하나가 모든 등급 값을 들고 있음. rolledRarity 를 함께 전송해
        /// 클라는 카드 value 표시, 호스트는 apply 시 같은 rarity 로 해석.
        /// </summary>
        private void SendStatBoostChoices(Photon.Realtime.Player player)
        {
            var db = GameManager.Instance?.StatBoostDB;
            if (db == null || db.All == null || db.All.Count == 0)
            {
                Debug.LogWarning($"[LevelUpManager] StatBoostDB 미연결 또는 비어있음 — 플레이어 {player.ActorNumber} 스킵.");
                playerSelections[player.ActorNumber] = true;
                return;
            }

            var rng = new System.Random();

            // Gambler bump (Phase 8-B3 소비자) — 파티 한 명이라도 도박꾼이면 base rolled 후 등급 상승.
            Rarity? gamblerOverride = ResolveGamblerOverride(rng, GetRarityWeights());

            var (choices, rolledRarity) = StatBoostChoiceService.GenerateChoices(
                db, 3, rng, rarityWeights: null, overrideRarity: gamblerOverride);
            if (choices.Length == 0)
            {
                Debug.LogWarning($"[LevelUpManager] StatBoost 선택지 생성 실패 — 플레이어 {player.ActorNumber} 스킵.");
                playerSelections[player.ActorNumber] = true;
                return;
            }

            int[] boostIds = new int[choices.Length];
            for (int i = 0; i < choices.Length; i++)
                boostIds[i] = choices[i].boostId;

            playerChoices[player.ActorNumber] = boostIds;
            playerPanelKinds[player.ActorNumber] = ChoicePanelKind.StatBoost;
            playerRolledRarities[player.ActorNumber] = rolledRarity;

            Debug.Log($"[LevelUpManager] → Player {player.ActorNumber} StatBoost 선택지({rolledRarity}): " +
                      string.Join(", ", System.Array.ConvertAll(choices, b => b.displayName)));

            photonView.RPC(nameof(RPC_ReceiveStatBoostChoices), player, boostIds, (int)rolledRarity);
        }

        private void SendChaosChoices(Photon.Realtime.Player player)
        {
            SkillManager sm = FindSkillManagerForPlayer(player.ActorNumber);
            if (sm == null)
            {
                playerSelections[player.ActorNumber] = true;
                return;
            }

            // Gambler bump (Phase 8-B3 소비자) — 파티 한 명이라도 도박꾼이면 base rolled 후 등급 상승.
            // 본 경로는 Lv.10/20/30 혼돈 선택지 — 자기 자신은 막 도박꾼을 처음 픽한 직후일 수 있어
            // 첫 선택 시점에는 효과 미적용 (장착 직후 Apply → 다음 레벨업부터 활성).
            var rng = new System.Random();
            Rarity? gamblerOverride = ResolveGamblerOverride(rng, GetRarityWeights());

            var (choices, rolledRarity) = sm.GenerateChaosChoices(
                skillDatabase.chaosSkills, 3, gamblerOverride);

            int[] choiceIds = new int[choices.Length];
            for (int i = 0; i < choices.Length; i++)
                choiceIds[i] = choices[i].skillId;

            playerChoices[player.ActorNumber] = choiceIds;
            playerPanelKinds[player.ActorNumber] = ChoicePanelKind.Chaos;
            playerRolledRarities[player.ActorNumber] = rolledRarity;

            photonView.RPC(nameof(RPC_ReceiveChoices), player,
                choiceIds, (int)ChoicePanelKind.Chaos, (int)rolledRarity);
        }

        // ===== Gambler bump (Phase 8-B3 소비자) =====

        /// <summary>
        /// 파티 전체에서 한 명이라도 도박꾼이 활성이면 baseline rarity 롤 → bump 적용 후 반환.
        /// 비활성이면 null 반환 → 호출부가 기본 weights 롤 사용.
        /// 호스트 전용 호출 (LevelUpManager.SendXxxChoices 가 호스트 권위).
        /// </summary>
        private static Rarity? ResolveGamblerOverride(System.Random rng, float[] weights)
        {
            if (!IsAnyPartyGambler()) return null;

            Rarity baseline = RarityWeightedRoller.Roll(weights, rng);
            Rarity bumped = GamblerRarityBumper.Bump(baseline, rng);
            // 인게임 검증용 — 효과가 비주얼로 즉각 드러나지 않으므로 콘솔에서 확인.
            Debug.Log($"[Gambler] 발동 — baseline:{baseline} → bumped:{bumped}");
            return bumped;
        }

        /// <summary>파티 한 명이라도 GamblerHandler 활성이면 true.</summary>
        private static bool IsAnyPartyGambler()
        {
            var players = GameObject.FindGameObjectsWithTag("Player");
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null) continue;
                var chaos = players[i].GetComponentInChildren<ChaosSkillManager>();
                if (chaos != null && chaos.IsGambler) return true;
            }
            return false;
        }

        /// <summary>등급 가중치 (Config 우선, 없으면 60/25/12/3 기본).</summary>
        private static float[] GetRarityWeights()
        {
            var cfg = GameManager.Instance?.Config;
            if (cfg != null && cfg.defaultRarityWeights != null && cfg.defaultRarityWeights.Length > 0)
                return cfg.defaultRarityWeights;
            return new float[] { 60f, 25f, 12f, 3f };
        }

        // ===== 클라이언트: 선택지 수신 =====

        [PunRPC]
        private void RPC_ReceiveChoices(int[] choiceIds, int panelKindInt, int rarityInt)
        {
            ChoicePanelKind kind = (ChoicePanelKind)panelKindInt;

            // Skill / Chaos 경로 전용. StatBoost 는 RPC_ReceiveStatBoostChoices 로 분리됨.
            myChoices = new SkillData[choiceIds.Length];
            for (int i = 0; i < choiceIds.Length; i++)
            {
                myChoices[i] = skillDatabase.GetSkillById(choiceIds[i]);
                if (myChoices[i] == null)
                    Debug.LogError($"[LevelUpManager] 스킬 ID {choiceIds[i]} 변환 실패!");
            }

            bool isChaos = kind == ChoicePanelKind.Chaos;
            myChaosRarity = isChaos ? (Rarity)rarityInt : Rarity.Common;

            Debug.Log($"[LevelUpManager] 선택지 수신 ({(isChaos ? $"혼돈/{myChaosRarity}" : "일반")}): " +
                      string.Join(", ", System.Array.ConvertAll(myChoices, s => s?.skillName ?? "null")));

            if (UIManager.Instance != null)
                UIManager.Instance.ShowLevelUp(myChoices, isChaos, myChaosRarity);
            else
                Debug.LogError("[LevelUpManager] UIManager.Instance 없음!");
        }

        /// <summary>
        /// StatBoost 전용 수신 RPC. rolledRarity 를 함께 받아 카드 value 표시 + apply 경로 공용.
        /// </summary>
        [PunRPC]
        private void RPC_ReceiveStatBoostChoices(int[] boostIds, int rolledRarityInt)
        {
            Rarity rolled = (Rarity)rolledRarityInt;
            myBoostRarity = rolled;

            var db = GameManager.Instance?.StatBoostDB;
            myBoostChoices = new StatBoostData[boostIds.Length];
            for (int i = 0; i < boostIds.Length; i++)
            {
                myBoostChoices[i] = db?.GetById(boostIds[i]);
                if (myBoostChoices[i] == null)
                    Debug.LogError($"[LevelUpManager] StatBoost ID {boostIds[i]} 변환 실패!");
            }

            Debug.Log($"[LevelUpManager] StatBoost 선택지 수신({rolled}): " +
                      string.Join(", ", System.Array.ConvertAll(myBoostChoices, b => b?.displayName ?? "null")));

            if (UIManager.Instance != null)
                UIManager.Instance.ShowLevelUpStatBoost(myBoostChoices, rolled);
            else
                Debug.LogError("[LevelUpManager] UIManager.Instance 없음!");
        }

        [PunRPC]
        private void RPC_StartTimer(float duration)
        {
            timeoutTimer = duration;
            isLevelUpActive = true;
        }

        // ===== 클라이언트: 선택 제출 =====

        /// <summary>
        /// LevelUpPanel에서 카드 클릭 시 호출.
        /// 선택한 스킬 ID를 호스트에 전송.
        /// </summary>
        public void SubmitChoice(int skillId)
        {
            if (!isLevelUpActive) return;

            int rarityInt = (int)myChaosRarity;
            Debug.Log($"[LevelUpManager] 선택 제출: 스킬 ID {skillId} (chaosRarity={myChaosRarity})");

            // 로컬에서 즉시 적용 (호스트 확인 전 — 반응성 우선).
            // Skill 일 때 myChaosRarity 는 Common(기본) 으로 SkillManager.ApplyChoice 에서 무시됨.
            if (localSkillManager != null)
            {
                SkillData chosen = skillDatabase.GetSkillById(skillId);
                if (chosen != null)
                    localSkillManager.ApplyChoice(chosen, myChaosRarity);
            }

            // 호스트에 알림 (rarityInt 는 혼돈일 때만 유효, 그 외 0).
            photonView.RPC(nameof(RPC_PlayerSelected), RpcTarget.MasterClient,
                PhotonNetwork.LocalPlayer.ActorNumber, skillId, rarityInt);
        }

        /// <summary>
        /// LevelUpPanel 에서 StatBoost 카드 클릭 시 호출.
        /// 선택한 boost ID를 호스트에 전송 (현재 패널의 rolledRarity 포함).
        /// </summary>
        public void SubmitBoostChoice(int boostId)
        {
            if (!isLevelUpActive) return;

            int rarityInt = (int)myBoostRarity;
            Debug.Log($"[LevelUpManager] StatBoost 선택 제출: ID {boostId} rarity={myBoostRarity}");

            // 로컬에서 즉시 적용 (반응성 우선).
            ApplyBoostLocally(PhotonNetwork.LocalPlayer.ActorNumber, boostId, myBoostRarity);

            photonView.RPC(nameof(RPC_PlayerBoostSelected), RpcTarget.MasterClient,
                PhotonNetwork.LocalPlayer.ActorNumber, boostId, rarityInt);
        }

        // ===== Phase 6: 퀘스트 보상 진입점 =====

        /// <summary>
        /// 퀘스트 완료 시 호스트가 호출. 모든 살아있는 플레이어에게 StatBoost 선택지를 보낸다.
        /// 게임을 일시정지하고 LevelUpManager 의 기존 시퀀스(타임아웃/재개)를 재사용.
        /// rarityWeights 가 비어 있으면 GameplayConfig.defaultRarityWeights 사용.
        /// </summary>
        public void RequestQuestReward(float[] rarityWeights)
        {
            if (!PhotonNetwork.IsMasterClient) return;

            // F2: 진행 중이면 큐에 적재 — EndLevelUpSequence 에서 pendingLevelUps 처리 후 dispatch.
            if (isLevelUpActive)
            {
                pendingQuestRewards.Enqueue(rarityWeights ?? System.Array.Empty<float>());
                Debug.Log($"[LevelUpManager] 레벨업/보상 진행 중 — 퀘스트 보상 큐 적재 (대기: {pendingQuestRewards.Count}건).");
                return;
            }

            StartQuestRewardSequence(rarityWeights);
        }

        /// <summary>RequestQuestReward 본체 — 큐잉 가드 통과 후 실제 시퀀스.</summary>
        private void StartQuestRewardSequence(float[] rarityWeights)
        {
            isLevelUpActive = true;
            timeoutTimer = selectionTimeout;
            playerSelections.Clear();
            playerChoices.Clear();
            playerPanelKinds.Clear();
            playerRolledRarities.Clear();

            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentState != GameManager.GameState.Paused)
                stateBeforeLevelUp = GameManager.Instance.CurrentState;
            GameManager.Instance?.ChangeStateNetwork(GameManager.GameState.Paused);

            foreach (var player in PhotonNetwork.PlayerList)
            {
                playerSelections[player.ActorNumber] = false;
                SendStatBoostChoicesWithWeights(player, rarityWeights);
            }

            photonView.RPC(nameof(RPC_StartTimer), RpcTarget.All, selectionTimeout);
            Debug.Log("[LevelUpManager] 퀘스트 보상 dispatch — StatBoost 선택지 송신 완료.");
        }

        /// <summary>
        /// SendStatBoostChoices 의 rarityWeights 오버라이드 버전.
        /// 퀘스트 보상은 자체 가중치를 가질 수 있으므로 분리.
        /// </summary>
        private void SendStatBoostChoicesWithWeights(Photon.Realtime.Player player, float[] rarityWeights)
        {
            var db = GameManager.Instance?.StatBoostDB;
            if (db == null || db.All == null || db.All.Count == 0)
            {
                playerSelections[player.ActorNumber] = true;
                return;
            }

            var rng = new System.Random();
            Rarity? gamblerOverride = ResolveGamblerOverride(rng, GetRarityWeights());

            float[] weights = (rarityWeights != null && rarityWeights.Length > 0)
                ? rarityWeights : null;

            var (choices, rolledRarity) = StatBoostChoiceService.GenerateChoices(
                db, 3, rng, rarityWeights: weights, overrideRarity: gamblerOverride);
            if (choices.Length == 0)
            {
                playerSelections[player.ActorNumber] = true;
                return;
            }

            int[] boostIds = new int[choices.Length];
            for (int i = 0; i < choices.Length; i++) boostIds[i] = choices[i].boostId;

            playerChoices[player.ActorNumber] = boostIds;
            playerPanelKinds[player.ActorNumber] = ChoicePanelKind.StatBoost;
            playerRolledRarities[player.ActorNumber] = rolledRarity;

            photonView.RPC(nameof(RPC_ReceiveStatBoostChoices), player, boostIds, (int)rolledRarity);
        }

        // ===== 호스트: 선택 결과 수신 =====

        [PunRPC]
        private void RPC_PlayerSelected(int actorNumber, int skillId, int rarityInt)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (!isLevelUpActive) return;

            // 이미 선택한 플레이어는 무시 (중복 방지)
            if (playerSelections.ContainsKey(actorNumber) && playerSelections[actorNumber])
                return;

            playerSelections[actorNumber] = true;

            Rarity rolledRarity = (Rarity)rarityInt;
            string skillName = skillDatabase.GetSkillById(skillId)?.skillName ?? "Unknown";
            Debug.Log($"[LevelUpManager] 플레이어 {actorNumber} 선택 완료: {skillName} rarity={rolledRarity}");

            // 호스트 자신이 아닌 원격 플레이어의 스킬 적용
            // (호스트는 SubmitChoice에서 이미 로컬 적용됨)
            if (actorNumber != PhotonNetwork.LocalPlayer.ActorNumber)
            {
                SkillManager sm = FindSkillManagerForPlayer(actorNumber);
                if (sm != null)
                {
                    SkillData chosen = skillDatabase.GetSkillById(skillId);
                    if (chosen != null)
                        sm.ApplyChoice(chosen, rolledRarity);
                }
            }

            // [Phase 5] 다른 클라이언트에도 동기화 — rarityInt 포함.
            photonView.RPC(nameof(RPC_SyncSkillAcquisition), RpcTarget.Others,
                actorNumber, skillId, rarityInt);

            // 전원 선택 완료 체크
            CheckAllSelected();
        }

        // ===== 호스트: 타임아웃 + 전원 완료 체크 =====

        private void Update()
        {
            if (!isLevelUpActive) return;

            // 타이머 갱신 (모든 클라이언트)
            if (timeoutTimer > 0f)
            {
                timeoutTimer -= Time.unscaledDeltaTime; // timeScale 영향 안 받도록
                // OnTimerUpdated?.Invoke(timeoutTimer, selectionTimeout);
                if (UIManager.Instance != null)
                    UIManager.Instance.UpdateLevelUpTimer(timeoutTimer, selectionTimeout);
            }

            // 타임아웃 처리 (호스트만)
            if (PhotonNetwork.IsMasterClient && timeoutTimer <= 0f)
            {
                HandleTimeout();
            }
        }

        /// <summary>
        /// 타임아웃 시 미선택 플레이어를 랜덤 선택 처리.
        /// 딕셔너리 순회 중 수정 방지를 위해 미선택 목록을 먼저 수집.
        /// </summary>
        private void HandleTimeout()
        {
            Debug.Log("[LevelUpManager] 타임아웃! 미선택 플레이어 랜덤 처리.");

            // 1) 미선택 플레이어 ActorNumber 수집 (순회 중 수정 방지)
            List<int> unselected = new List<int>();
            foreach (var kvp in playerSelections)
            {
                if (!kvp.Value)
                    unselected.Add(kvp.Key);
            }

            // 2) 수집한 목록으로 처리
            for (int i = 0; i < unselected.Count; i++)
            {
                int actorNumber = unselected[i];

                if (!playerChoices.TryGetValue(actorNumber, out int[] choices) || choices.Length == 0)
                {
                    playerSelections[actorNumber] = true;
                    continue;
                }

                int randomId = choices[Random.Range(0, choices.Length)];
                ChoicePanelKind kind = playerPanelKinds.TryGetValue(actorNumber, out var k)
                    ? k : ChoicePanelKind.Skill;

                Debug.Log($"[LevelUpManager] 플레이어 {actorNumber} 랜덤 선택({kind}): ID {randomId}");

                if (kind == ChoicePanelKind.StatBoost)
                {
                    // StatBoost 분기: 저장된 rolledRarity 로 value 해석. 호스트 로컬 적용 + sync.
                    Rarity rarity = playerRolledRarities.TryGetValue(actorNumber, out var rr)
                        ? rr : Rarity.Common;
                    int rarityInt = (int)rarity;

                    ApplyBoostLocally(actorNumber, randomId, rarity);

                    if (actorNumber != PhotonNetwork.LocalPlayer.ActorNumber)
                    {
                        var targetPlayer = FindPhotonPlayer(actorNumber);
                        if (targetPlayer != null)
                            photonView.RPC(nameof(RPC_ForceBoostChoice), targetPlayer, randomId, rarityInt);
                    }

                    photonView.RPC(nameof(RPC_SyncBoostAcquisition), RpcTarget.Others,
                        actorNumber, randomId, rarityInt);
                }
                else
                {
                    // Skill / Chaos 분기. Chaos 면 저장된 rolledRarity 로 apply.
                    Rarity rarity = playerRolledRarities.TryGetValue(actorNumber, out var rr)
                        ? rr : Rarity.Common;
                    int rarityInt = (int)rarity;

                    SkillManager sm = FindSkillManagerForPlayer(actorNumber);
                    if (sm != null)
                    {
                        SkillData chosen = skillDatabase.GetSkillById(randomId);
                        if (chosen != null)
                            sm.ApplyChoice(chosen, rarity);
                    }

                    if (actorNumber != PhotonNetwork.LocalPlayer.ActorNumber)
                    {
                        var targetPlayer = FindPhotonPlayer(actorNumber);
                        if (targetPlayer != null)
                            photonView.RPC(nameof(RPC_ForceChoice), targetPlayer, randomId, rarityInt);
                    }

                    photonView.RPC(nameof(RPC_SyncSkillAcquisition), RpcTarget.Others,
                        actorNumber, randomId, rarityInt);
                }

                playerSelections[actorNumber] = true;
            }

            // 전원 완료 → 게임 재개
            EndLevelUpSequence();
        }

        /// <summary>
        /// 전원 선택 완료 여부 체크. 호스트에서만.
        /// </summary>
        private void CheckAllSelected()
        {
            foreach (var kvp in playerSelections)
            {
                if (!kvp.Value) return; // 아직 안 고른 사람 있음
            }

            Debug.Log("[LevelUpManager] 전원 선택 완료!");
            EndLevelUpSequence();
        }

        // ===== 게임 재개 =====

        private void EndLevelUpSequence()
        {
            isLevelUpActive = false;

            // Phase 6: 마지막 혼돈 레벨 선택 완료 → 보스 혼돈 스킬 결정
            var cfg = GameManager.Instance?.Config;
            if (PhotonNetwork.IsMasterClient && cfg != null
                && cfg.chaosLevels.Length > 0
                && currentProcessingLevel == cfg.chaosLevels[cfg.chaosLevels.Length - 1])
            {
                if (BossChaosApplicator.Instance != null)
                    BossChaosApplicator.Instance.DetermineBossChaosSkill();
            }

            playerSelections.Clear();
            playerChoices.Clear();
            playerPanelKinds.Clear();
            playerRolledRarities.Clear();

            // 대기 중인 레벨업이 있으면 바로 다음 시퀀스 시작
            if (pendingLevelUps.Count > 0)
            {
                int nextLevel = pendingLevelUps.Dequeue();
                currentProcessingLevel = nextLevel;
                Debug.Log($"[LevelUpManager] 대기열 레벨업 처리: Lv.{nextLevel} (남은 대기: {pendingLevelUps.Count}개)");

                bool isChaosLevel = GameManager.Instance?.Config != null
                    && GameManager.Instance.Config.IsChaosLevel(nextLevel);
                StartLevelUpSequence(isChaosLevel);

                // UI 닫기 → 새 선택지 UI 열기 (클라이언트에서 자연스럽게 전환)
                // photonView.RPC(nameof(RPC_LevelUpEnded), RpcTarget.All);
                StartLevelUpSequenceInternal(isChaosLevel);

                return;
            }

            // F2: 레벨업 큐 비었으면 보류된 퀘스트 보상 처리.
            if (PhotonNetwork.IsMasterClient && pendingQuestRewards.Count > 0)
            {
                float[] weights = pendingQuestRewards.Dequeue();
                Debug.Log($"[LevelUpManager] 대기열 퀘스트 보상 처리 (남은 대기: {pendingQuestRewards.Count}건)");
                StartQuestRewardSequence(weights.Length == 0 ? null : weights);
                return;
            }

            // 게임 재개
            // 호스트 마이그레이션 후 레벨업 완료 → GameTime 기준 게임 재개
            if (HostMigrationHandler.Instance != null &&
                HostMigrationHandler.Instance.PendingGameResume)
            {
                // 마이그레이션 대기 중 → 이전 상태 복원 대신 ResumeGameFromCurrentTime 실행
                HostMigrationHandler.Instance.OnLevelUpCompleted();
            }
            else
            {
                // 정상 플로우: 이전 상태로 복원
                GameManager.Instance.ChangeStateNetwork(stateBeforeLevelUp);
            }

            // UI 닫기 알림
            photonView.RPC(nameof(RPC_LevelUpEnded), RpcTarget.All);
        }

        /// <summary>
        /// 호스트 마이그레이션 시 레벨업 세션 인수.
        /// 클라이언트 선택지는 유지, 새 호스트가 선택 결과 수신 준비.
        /// </summary>
        public void AdoptLevelUpSession()
        {
            pendingLevelUps.Clear();

            isLevelUpActive = true;
            playerSelections.Clear();
            playerChoices.Clear();
            playerPanelKinds.Clear();
            playerRolledRarities.Clear();

            // 레벨업 완료 후 복원할 상태 (Playing으로 복원 → 이후 비상 보스전이 BossFight로 전환)
            stateBeforeLevelUp = GameManager.GameState.Playing;

            foreach (var player in PhotonNetwork.PlayerList)
            {
                playerSelections[player.ActorNumber] = false;
            }

            timeoutTimer = selectionTimeout;

            Debug.Log($"[LevelUpManager] 레벨업 세션 인수 — {playerSelections.Count}명 대기");
        }

        [PunRPC]
        private void RPC_LevelUpEnded()
        {
            isLevelUpActive = false;
            myChoices = null;
            // OnLevelUpEnded?.Invoke();

            if (UIManager.Instance != null)
                UIManager.Instance.HideLevelUp();

            Debug.Log("[LevelUpManager] 레벨업 종료, 게임 재개");
        }

        /// <summary>
        /// 타임아웃 시 호스트가 강제 선택한 결과를 클라이언트에 알림.
        /// </summary>
        [PunRPC]
        private void RPC_ForceChoice(int skillId, int rarityInt)
        {
            Rarity r = (Rarity)rarityInt;
            Debug.Log($"[LevelUpManager] 타임아웃 → 랜덤 선택됨: ID {skillId} rarity={r}");

            // 로컬 적용 — 혼돈이면 rarity 반영.
            if (localSkillManager != null)
            {
                SkillData chosen = skillDatabase.GetSkillById(skillId);
                if (chosen != null)
                    localSkillManager.ApplyChoice(chosen, r);
            }
        }

        // ===== StatBoost 경로 (호스트) =====

        [PunRPC]
        private void RPC_PlayerBoostSelected(int actorNumber, int boostId, int rarityInt)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (!isLevelUpActive) return;

            if (playerSelections.ContainsKey(actorNumber) && playerSelections[actorNumber])
                return;

            playerSelections[actorNumber] = true;

            Rarity rarity = (Rarity)rarityInt;
            var data = GameManager.Instance?.StatBoostDB?.GetById(boostId);
            string name = data?.displayName ?? "Unknown";
            Debug.Log($"[LevelUpManager] Player {actorNumber} StatBoost 선택 완료: {name}({rarity})");

            // 호스트 자신은 SubmitBoostChoice 에서 이미 로컬 적용됨 → 원격 플레이어만 적용.
            if (actorNumber != PhotonNetwork.LocalPlayer.ActorNumber)
                ApplyBoostLocally(actorNumber, boostId, rarity);

            // 다른 클라이언트에도 동기화 (본인 클라는 SubmitBoostChoice 에서 이미 적용했으므로 Others).
            photonView.RPC(nameof(RPC_SyncBoostAcquisition), RpcTarget.Others,
                actorNumber, boostId, rarityInt);

            CheckAllSelected();
        }

        [PunRPC]
        private void RPC_SyncBoostAcquisition(int actorNumber, int boostId, int rarityInt)
        {
            // 본인은 SubmitBoostChoice 에서 이미 적용 — 스킵.
            if (actorNumber == PhotonNetwork.LocalPlayer.ActorNumber) return;
            ApplyBoostLocally(actorNumber, boostId, (Rarity)rarityInt);
        }

        [PunRPC]
        private void RPC_ForceBoostChoice(int boostId, int rarityInt)
        {
            Rarity rarity = (Rarity)rarityInt;
            Debug.Log($"[LevelUpManager] 타임아웃 → StatBoost 랜덤 선택됨: ID {boostId} ({rarity})");
            ApplyBoostLocally(PhotonNetwork.LocalPlayer.ActorNumber, boostId, rarity);
        }

        /// <summary>
        /// 지정 ActorNumber 의 StatBoostManager 에 boost 적용 (로컬 경로).
        /// Submit/Sync/ForceChoice 공통 호출. rarity 는 value 해석용.
        /// </summary>
        private void ApplyBoostLocally(int actorNumber, int boostId, Rarity rarity)
        {
            var data = GameManager.Instance?.StatBoostDB?.GetById(boostId);
            if (data == null)
            {
                Debug.LogError($"[LevelUpManager] StatBoost ID {boostId} 해결 실패.");
                return;
            }

            var mgr = FindStatBoostManagerForPlayer(actorNumber);
            if (mgr == null)
            {
                Debug.LogWarning($"[LevelUpManager] Actor {actorNumber} StatBoostManager 미발견.");
                return;
            }
            mgr.ApplyChoice(data, rarity);
        }

        private StatBoostManager FindStatBoostManagerForPlayer(int actorNumber)
        {
            var players = GameObject.FindGameObjectsWithTag("Player");
            foreach (var playerObj in players)
            {
                PhotonView pv = playerObj.GetComponent<PhotonView>();
                if (pv != null && pv.Owner != null && pv.Owner.ActorNumber == actorNumber)
                    return playerObj.GetComponentInChildren<StatBoostManager>();
            }
            return null;
        }

        // ===== 스킬 동기화 (Phase 5) =====

        /// <summary>
        /// 호스트가 선택 확정 후 다른 클라이언트에 브로드캐스트.
        /// 각 클라이언트에서 해당 플레이어의 SkillManager에 스킬 적용.
        /// → 모든 클라이언트가 모든 플레이어의 스킬 이펙트를 로컬 실행.
        /// </summary>
        [PunRPC]
        private void RPC_SyncSkillAcquisition(int actorNumber, int skillId, int rarityInt)
        {
            // 자기 자신은 SubmitChoice에서 이미 적용했으므로 스킵
            if (actorNumber == PhotonNetwork.LocalPlayer.ActorNumber)
                return;

            SkillManager sm = FindSkillManagerForPlayer(actorNumber);
            if (sm == null)
            {
                Debug.LogWarning($"[LevelUpManager] Sync 실패 — Actor {actorNumber}의 SkillManager 없음");
                return;
            }

            SkillData chosen = skillDatabase.GetSkillById(skillId);
            if (chosen != null)
            {
                sm.ApplyChoice(chosen, (Rarity)rarityInt);
                Debug.Log($"[LevelUpManager] Sync 완료 — Actor {actorNumber}: {chosen.skillName}");
            }
        }

        // ===== 유틸리티 =====

        /// <summary>
        /// ActorNumber로 해당 플레이어의 SkillManager 찾기.
        /// PhotonView.Owner를 기준으로 탐색.
        /// </summary>
        private SkillManager FindSkillManagerForPlayer(int actorNumber)
        {
            // 모든 Player 태그 오브젝트에서 PhotonView 확인
            var players = GameObject.FindGameObjectsWithTag("Player");
            foreach (var playerObj in players)
            {
                PhotonView pv = playerObj.GetComponent<PhotonView>();
                if (pv != null && pv.Owner != null && pv.Owner.ActorNumber == actorNumber)
                {
                    return playerObj.GetComponentInChildren<SkillManager>();
                }
            }

            Debug.LogWarning($"[LevelUpManager] ActorNumber {actorNumber}의 SkillManager 못 찾음");
            return null;
        }

        /// <summary>
        /// ActorNumber로 Photon.Realtime.Player 찾기.
        /// </summary>
        private Photon.Realtime.Player FindPhotonPlayer(int actorNumber)
        {
            foreach (var player in PhotonNetwork.PlayerList)
            {
                if (player.ActorNumber == actorNumber)
                    return player;
            }
            return null;
        }

        // ===== 디버그 =====

        /// <summary>
        /// 현재 레벨업 상태 정보 (디버그용).
        /// </summary>
        public bool IsLevelUpActive => isLevelUpActive;
        public float TimeRemaining => timeoutTimer;

        /// <summary>
        /// 디버그: 선택지 수신 상태 확인.
        /// </summary>
        public SkillData[] GetCurrentChoices() => myChoices;
    }
}