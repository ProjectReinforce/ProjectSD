using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using SwDreams.Domain;
using SwDreams.Data;
using SwDreams.Adapter.Skill;
using SwDreams.Presentation;
using Adapter.Manager;
using Adapter.UI.Menu;

namespace SwDreams.Adapter.Manager
{
    /// <summary>
    /// 게임 결과 수집 + RPC 전송 + 씬 전환.
    ///
    /// 플로우:
    /// 1. GameState → GameClear/GameOver 감지
    /// 2. 각 클라이언트: 로컬 빌드 데이터를 호스트에 RPC 전송
    /// 3. 호스트: 팀 통계 + 전체 빌드 수집 → 결과 RPC 브로드캐스트
    /// 4. 모든 클라이언트: ResultPanelUI 표시
    /// 5. 다시 하기 / 나가기 처리
    ///
    /// 셋업: GameScene에 빈 오브젝트 → ResultManager + PhotonView 부착.
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class ResultManager : MonoBehaviourPun
    {
        public static ResultManager Instance { get; private set; }

        // 호스트: 수집된 빌드 데이터
        private Dictionary<int, PlayerBuildData> collectedBuilds = new();
        private int expectedPlayerCount;
        private bool resultBroadcasted = false;

        // 로컬: 결과 데이터 (UI 표시용)
        private GameResult localResult;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            if (GameManager.Instance != null)
                GameManager.Instance.OnStateChanged -= OnGameStateChanged;
        }

        private void Start()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnStateChanged += OnGameStateChanged;
        }

        // ===== 게임 종료 감지 =====

        private void OnGameStateChanged(GameManager.GameState newState)
        {
            if (newState != GameManager.GameState.GameClear &&
                newState != GameManager.GameState.GameOver)
                return;

            Debug.Log($"[ResultManager] 게임 종료 감지: {newState}");

            // 호스트: 수집 준비 (Clear를 먼저 해야 SendLocalBuildToHost의 결과가 날아가지 않음)
            if (PhotonNetwork.IsMasterClient)
            {
                resultBroadcasted = false;
                expectedPlayerCount = PhotonNetwork.PlayerList.Length;
                collectedBuilds.Clear();

                // 2초 후 강제 브로드캐스트 (빌드 미도착 클라이언트 대비)
                Invoke(nameof(ForceBroadcastResult), 2f);
            }

            // 각 클라이언트: 로컬 빌드를 호스트에 전송
            SendLocalBuildToHost();
        }

        // ===== 로컬 빌드 수집 + 전송 =====

        private void SendLocalBuildToHost()
        {
            var localPlayer = FindLocalPlayerStub();
            if (localPlayer == null) return;

            var skillManager = localPlayer.GetComponentInChildren<SkillManager>();
            var chaosManager = localPlayer.GetComponentInChildren<ChaosSkillManager>();

            // 스킬 ID + 레벨 수집
            int[] skillIds = System.Array.Empty<int>();
            int[] skillLevels = System.Array.Empty<int>();
            if (skillManager != null)
            {
                var equipped = skillManager.EquippedSkills;
                skillIds = new int[equipped.Count];
                skillLevels = new int[equipped.Count];
                for (int i = 0; i < equipped.Count; i++)
                {
                    skillIds[i] = equipped[i].Data.skillId;
                    skillLevels[i] = equipped[i].Level;
                }
            }

            // 혼돈 스킬 수집
            int[] chaosIds = System.Array.Empty<int>();
            if (chaosManager != null)
            {
                var effects = chaosManager.ActiveEffects;
                chaosIds = new int[effects.Count];
                for (int i = 0; i < effects.Count; i++)
                    chaosIds[i] = (int)effects[i];
            }

            // 캐릭터 ID
            int characterId = -1;
            if (localPlayer.CharacterData != null)
                characterId = localPlayer.CharacterData.id;

            string playerName = PhotonNetwork.LocalPlayer.NickName;
            if (string.IsNullOrEmpty(playerName))
                playerName = $"Player {PhotonNetwork.LocalPlayer.ActorNumber}";

            photonView.RPC(nameof(RPC_SendBuildToHost), RpcTarget.MasterClient,
                PhotonNetwork.LocalPlayer.ActorNumber,
                playerName,
                characterId,
                skillIds,
                skillLevels,
                chaosIds);
        }

        [PunRPC]
        private void RPC_SendBuildToHost(int actorNumber, string playerName,
            int characterId, int[] skillIds, int[] skillLevels, int[] chaosIds)
        {
            if (!PhotonNetwork.IsMasterClient) return;

            var build = new PlayerBuildData
            {
                ActorNumber = actorNumber,
                PlayerName = playerName,
                CharacterId = characterId,
                SkillIds = skillIds,
                SkillLevels = skillLevels,
                ChaosTypeIds = chaosIds
            };

            collectedBuilds[actorNumber] = build;
            Debug.Log($"[ResultManager] 빌드 수신: {playerName} (스킬 {skillIds.Length}개)");

            // 모든 플레이어 빌드 수집 완료 → 결과 브로드캐스트
            if (collectedBuilds.Count >= expectedPlayerCount)
                BroadcastResult();
        }

        // ===== 결과 브로드캐스트 =====

        private void ForceBroadcastResult()
        {
            if (!resultBroadcasted)
            {
                Debug.Log("[ResultManager] 타임아웃 — 수집된 빌드로 결과 브로드캐스트");
                BroadcastResult();
            }
        }

        private void BroadcastResult()
        {
            if (!PhotonNetwork.IsMasterClient || resultBroadcasted) return;
            resultBroadcasted = true;
            CancelInvoke(nameof(ForceBroadcastResult));

            bool isCleared = GameManager.Instance.CurrentState == GameManager.GameState.GameClear;
            float playTime = GameManager.Instance.GameTime;
            int teamLevel = GameManager.Instance.TeamLevel;
            int totalKills = GameStatTracker.Instance != null ? GameStatTracker.Instance.TotalKills : 0;
            int totalDeaths = GameStatTracker.Instance != null ? GameStatTracker.Instance.TotalDeaths : 0;
            int bossChaos = BossChaosApplicator.Instance != null
                ? (int)BossChaosApplicator.Instance.BossChaosType : 0;

            // 빌드 데이터를 직렬화 (int[] 배열로 flatten)
            // 형식: [playerCount, (actorNum, charId, skillCount, ...skillIds, ...skillLevels, chaosCount, ...chaosIds, nameLength, ...nameChars) × N]
            List<int> buildPayload = new List<int>
            {
                collectedBuilds.Count
            };

            foreach (var kvp in collectedBuilds)
            {
                var b = kvp.Value;
                buildPayload.Add(b.ActorNumber);
                buildPayload.Add(b.CharacterId);
                buildPayload.Add(b.SkillIds?.Length ?? 0);
                if (b.SkillIds != null)
                {
                    buildPayload.AddRange(b.SkillIds);
                    buildPayload.AddRange(b.SkillLevels);
                }
                buildPayload.Add(b.ChaosTypeIds?.Length ?? 0);
                if (b.ChaosTypeIds != null)
                    buildPayload.AddRange(b.ChaosTypeIds);

                // 이름: 길이 + char 코드
                string name = b.PlayerName ?? "";
                buildPayload.Add(name.Length);
                foreach (char c in name)
                    buildPayload.Add(c);
            }

            photonView.RPC(nameof(RPC_ShowResult), RpcTarget.All,
                isCleared, playTime, teamLevel, totalKills, totalDeaths,
                bossChaos, buildPayload.ToArray());
        }

        [PunRPC]
        private void RPC_ShowResult(bool isCleared, float playTime, int teamLevel,
            int totalKills, int totalDeaths, int bossChaos, int[] buildPayload)
        {
            // GameResult 역직렬화
            localResult = new GameResult
            {
                IsCleared = isCleared,
                PlayTime = playTime,
                TeamLevel = teamLevel,
                TotalKills = totalKills,
                TotalDeaths = totalDeaths,
                BossChaosTypeId = bossChaos
            };

            // 빌드 데이터 역직렬화
            localResult.PlayerBuilds = DeserializeBuilds(buildPayload);

            Debug.Log($"[ResultManager] 결과 수신: {(isCleared ? "클리어" : "실패")} " +
                      $"Time:{playTime:F1}s Lv:{teamLevel} Kills:{totalKills}");

            // UI 표시
            UIManager.Instance?.ShowResult(localResult);
        }

        private PlayerBuildData[] DeserializeBuilds(int[] payload)
        {
            if (payload == null || payload.Length == 0)
                return System.Array.Empty<PlayerBuildData>();

            int idx = 0;
            int playerCount = payload[idx++];
            var builds = new PlayerBuildData[playerCount];

            for (int p = 0; p < playerCount; p++)
            {
                var b = new PlayerBuildData();
                b.ActorNumber = payload[idx++];
                b.CharacterId = payload[idx++];

                int skillCount = payload[idx++];
                b.SkillIds = new int[skillCount];
                b.SkillLevels = new int[skillCount];
                for (int i = 0; i < skillCount; i++)
                    b.SkillIds[i] = payload[idx++];
                for (int i = 0; i < skillCount; i++)
                    b.SkillLevels[i] = payload[idx++];

                int chaosCount = payload[idx++];
                b.ChaosTypeIds = new int[chaosCount];
                for (int i = 0; i < chaosCount; i++)
                    b.ChaosTypeIds[i] = payload[idx++];

                int nameLen = payload[idx++];
                char[] nameChars = new char[nameLen];
                for (int i = 0; i < nameLen; i++)
                    nameChars[i] = (char)payload[idx++];
                b.PlayerName = new string(nameChars);

                builds[p] = b;
            }

            return builds;
        }

        // ===== 씬 전환 =====

        /// <summary>
        /// 다시 하기 버튼. 각 클라이언트가 독립적으로 처리.
        /// 방에 남은 채 MenuScene으로 이동 → 대기실 표시.
        /// </summary>
        public void OnRetry()
        {
            // 씬 동기화 해제 (각 클라이언트가 독립적으로 씬 전환하기 위해)
            PhotonNetwork.AutomaticallySyncScene = false;

            // ready 초기화
            NetworkManager.Instance?.SetLocalReady(false);

            // 대기실로 복귀하므로 로비 목록에 다시 표시
            if (PhotonNetwork.IsMasterClient
                && PhotonNetwork.CurrentRoom != null)
            {
                PhotonNetwork.CurrentRoom.IsVisible = true;
                PhotonNetwork.CurrentRoom.IsOpen = true;
            }
            
            UnityEngine.SceneManagement.SceneManager.LoadScene("MenuScene");
            Debug.Log("[ResultManager] 다시 하기 → MenuScene (방 유지)");
        }

        /// <summary>
        /// 나가기 버튼. 방 퇴장 후 MenuScene으로 이동 → 방 리스트 표시.
        /// </summary>
        public void OnExit()
        {
            // 씬 동기화 해제
            PhotonNetwork.AutomaticallySyncScene = false;

            // MenuScene 진입 시 방 리스트로 바로 가도록 플래그 설정
            MenuSceneManager.ReturnToRoomList = true;

            if (NetworkManager.Instance != null && PhotonNetwork.InRoom)
            {
                NetworkManager.Instance.LeftRoom += OnLeftRoomForExit;
                NetworkManager.Instance.LeaveRoom();
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("MenuScene");
            }
            Debug.Log("[ResultManager] 나가기 요청 -> 방 리스트로 이동");
        }

        private void OnLeftRoomForExit()
        {
            NetworkManager.Instance.LeftRoom -= OnLeftRoomForExit;
            UnityEngine.SceneManagement.SceneManager.LoadScene("MenuScene");
            Debug.Log("[ResultManager] 방 퇴장 완료 → MenuScene");
        }

        // ===== 유틸리티 =====

        private SwDreams.Testing.PlayerStub FindLocalPlayerStub()
        {
            var players = GameObject.FindGameObjectsWithTag("Player");
            foreach (var go in players)
            {
                var pv = go.GetComponent<PhotonView>();
                if (pv != null && pv.IsMine)
                    return go.GetComponent<SwDreams.Testing.PlayerStub>();
            }
            return null;
        }
    }
}