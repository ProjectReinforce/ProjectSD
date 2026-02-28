using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using SwDreams.Data;
using SwDreams.Adapter.Skill;
using SwDreams.Presentation;

namespace SwDreams.Adapter.Manager
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

        // 레벨업 큐: 진행 중일 때 추가 레벨업이 발생하면 대기열에 저장
        private Queue<int> pendingLevelUps = new Queue<int>();

        // 호스트 전용: 각 플레이어의 선택 완료 여부
        // key = Photon ActorNumber, value = 선택 완료 여부
        private Dictionary<int, bool> playerSelections = new Dictionary<int, bool>();

        // 호스트 전용: 각 플레이어에게 보낸 선택지 (타임아웃 시 랜덤 선택용)
        // key = ActorNumber, value = 선택지 스킬 ID 배열
        private Dictionary<int, int[]> playerChoices = new Dictionary<int, int[]>();

        // 클라이언트: 내가 받은 선택지
        private SkillData[] myChoices;

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

            bool isChaosLevel = (newLevel == 5 || newLevel == 10 || newLevel == 15);
            StartLevelUpSequence(isChaosLevel);
        }

        // ===== 호스트: 레벨업 시퀀스 시작 =====

        private void StartLevelUpSequence(bool isChaosLevel)
        {
            isLevelUpActive = true;
            timeoutTimer = selectionTimeout;
            playerSelections.Clear();
            playerChoices.Clear();

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

            // 스킬 ID 배열로 변환 (RPC는 SO를 직접 못 보내므로)
            int[] choiceIds = new int[choices.Length];
            for (int i = 0; i < choices.Length; i++)
                choiceIds[i] = choices[i].skillId;

            // 저장 (타임아웃 시 랜덤 선택용)
            playerChoices[player.ActorNumber] = choiceIds;

            Debug.Log($"[LevelUpManager] → Player {player.ActorNumber} 선택지: " +
                    string.Join(", ", System.Array.ConvertAll(choices, s => s.skillName)));

            // 해당 플레이어에게만 RPC 전송
            photonView.RPC(nameof(RPC_ReceiveChoices), player, choiceIds, false);
        }

        private void SendChaosChoices(Photon.Realtime.Player player)
        {
            SkillManager sm = FindSkillManagerForPlayer(player.ActorNumber);
            if (sm == null)
            {
                playerSelections[player.ActorNumber] = true;
                return;
            }

            SkillData[] choices = sm.GenerateChaosChoices(skillDatabase.chaosSkills, 3);

            int[] choiceIds = new int[choices.Length];
            for (int i = 0; i < choices.Length; i++)
                choiceIds[i] = choices[i].skillId;

            playerChoices[player.ActorNumber] = choiceIds;
            photonView.RPC(nameof(RPC_ReceiveChoices), player, choiceIds, true);
        }

        // ===== 클라이언트: 선택지 수신 =====

        [PunRPC]
        private void RPC_ReceiveChoices(int[] choiceIds, bool isChaos)
        {
            // 스킬 ID → SkillData 변환
            myChoices = new SkillData[choiceIds.Length];
            for (int i = 0; i < choiceIds.Length; i++)
            {
                myChoices[i] = skillDatabase.GetSkillById(choiceIds[i]);
                if (myChoices[i] == null)
                    Debug.LogError($"[LevelUpManager] 스킬 ID {choiceIds[i]} 변환 실패!");
            }

            Debug.Log($"[LevelUpManager] 선택지 수신 ({(isChaos ? "혼돈" : "일반")}): " +
                      string.Join(", ", System.Array.ConvertAll(myChoices, s => s?.skillName ?? "null")));

            // UI 이벤트 발행
            if (UIManager.Instance != null)
                UIManager.Instance.ShowLevelUp(myChoices, isChaos);
            else
                Debug.LogError("[LevelUpManager] UIManager.Instance 없음!");
            // if (isChaos)
            //     OnChaosChoicesReceived?.Invoke(myChoices);
            // else
            //     OnChoicesReceived?.Invoke(myChoices);
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

            Debug.Log($"[LevelUpManager] 선택 제출: 스킬 ID {skillId}");

            // 로컬에서 즉시 적용 (호스트 확인 전 — 반응성 우선)
            if (localSkillManager != null)
            {
                SkillData chosen = skillDatabase.GetSkillById(skillId);
                if (chosen != null)
                    localSkillManager.ApplyChoice(chosen);
            }

            // 호스트에 알림
            photonView.RPC(nameof(RPC_PlayerSelected), RpcTarget.MasterClient,
                PhotonNetwork.LocalPlayer.ActorNumber, skillId);
        }

        // ===== 호스트: 선택 결과 수신 =====

        [PunRPC]
        private void RPC_PlayerSelected(int actorNumber, int skillId)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (!isLevelUpActive) return;

            // 이미 선택한 플레이어는 무시 (중복 방지)
            if (playerSelections.ContainsKey(actorNumber) && playerSelections[actorNumber])
                return;

            playerSelections[actorNumber] = true;

            string skillName = skillDatabase.GetSkillById(skillId)?.skillName ?? "Unknown";
            Debug.Log($"[LevelUpManager] 플레이어 {actorNumber} 선택 완료: {skillName}");

            // 호스트 자신이 아닌 원격 플레이어의 스킬 적용
            // (호스트는 SubmitChoice에서 이미 로컬 적용됨)
            if (actorNumber != PhotonNetwork.LocalPlayer.ActorNumber)
            {
                SkillManager sm = FindSkillManagerForPlayer(actorNumber);
                if (sm != null)
                {
                    SkillData chosen = skillDatabase.GetSkillById(skillId);
                    if (chosen != null)
                        sm.ApplyChoice(chosen);
                }
            }

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

                if (playerChoices.TryGetValue(actorNumber, out int[] choices) && choices.Length > 0)
                {
                    int randomId = choices[Random.Range(0, choices.Length)];
                    Debug.Log($"[LevelUpManager] 플레이어 {actorNumber} 랜덤 선택: ID {randomId}");

                    SkillManager sm = FindSkillManagerForPlayer(actorNumber);
                    if (sm != null)
                    {
                        SkillData chosen = skillDatabase.GetSkillById(randomId);
                        if (chosen != null)
                            sm.ApplyChoice(chosen);
                    }

                    Photon.Realtime.Player targetPlayer = FindPhotonPlayer(actorNumber);
                    if (targetPlayer != null)
                    {
                        photonView.RPC(nameof(RPC_ForceChoice), targetPlayer, randomId);
                    }
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
            playerSelections.Clear();
            playerChoices.Clear();

            // 대기 중인 레벨업이 있으면 바로 다음 시퀀스 시작
            if (pendingLevelUps.Count > 0)
            {
                int nextLevel = pendingLevelUps.Dequeue();
                Debug.Log($"[LevelUpManager] 대기열 레벨업 처리: Lv.{nextLevel} (남은 대기: {pendingLevelUps.Count}개)");

                bool isChaosLevel = (nextLevel == 5 || nextLevel == 10 || nextLevel == 15);
                StartLevelUpSequence(isChaosLevel);

                // UI 닫기 → 새 선택지 UI 열기 (클라이언트에서 자연스럽게 전환)
                // photonView.RPC(nameof(RPC_LevelUpEnded), RpcTarget.All);
                StartLevelUpSequenceInternal(isChaosLevel);
                
                return;
            }

            // 게임 재개
            GameManager.Instance.ChangeStateNetwork(GameManager.GameState.Playing);

            // UI 닫기 알림
            photonView.RPC(nameof(RPC_LevelUpEnded), RpcTarget.All);
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
        private void RPC_ForceChoice(int skillId)
        {
            Debug.Log($"[LevelUpManager] 타임아웃 → 랜덤 선택됨: ID {skillId}");

            // 로컬 적용
            if (localSkillManager != null)
            {
                SkillData chosen = skillDatabase.GetSkillById(skillId);
                if (chosen != null)
                    localSkillManager.ApplyChoice(chosen);
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