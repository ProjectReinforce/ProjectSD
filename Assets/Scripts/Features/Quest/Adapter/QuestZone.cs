using System.Collections.Generic;
using Photon.Pun;
using SwDreams.Features.Quest.Adapter.Data;
using SwDreams.Features.Quest.Domain;
using SwDreams.Shared.Managers;
using UnityEngine;

namespace SwDreams.Features.Quest.Adapter
{
    /// <summary>
    /// 퀘스트 거점. 호스트 권위 상태 머신.
    ///
    /// 셋업:
    /// - GameScene 또는 맵 prefab 에 빈 GameObject + QuestZone + PhotonView 부착.
    /// - data 인스펙터에 QuestData SO 할당.
    /// - 시각 마커(빛 기둥 등)는 자식 prefab 으로 별도 배치.
    ///
    /// 흐름 (호스트):
    /// 1. Idle: 매 프레임 모든 살아있는 플레이어가 triggerRadius 내인지 체크.
    ///    충족 → Waiting 진입 + RPC 동기화.
    /// 2. Waiting: data.waitTime 카운트다운. 도중 이탈 → Idle 리셋.
    /// 3. InProgress: 격리 몹 스폰 (TODO MVP 외) + targetCount 추적. KillTarget 만 우선 구현.
    /// 4. Completed/Failed: 격리 몹 정리, 보상 dispatch (Completed), 거점 비활성.
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class QuestZone : MonoBehaviourPun
    {
        [Header("데이터")]
        [SerializeField] private QuestData data;

        [Header("디버그")]
        [SerializeField] private bool drawGizmos = true;

        // ===== 활성 InProgress QuestZone 레지스트리 =====
        // Enemy.OnDeath 등 외부에서 NotifyTargetKilled 통지할 때 사용.
        // KillTarget 케이스: 모든 적 사망 → 활성 zone 전체에 통지 (필터는 zone 내부에서).
        private static readonly List<QuestZone> activeZones = new List<QuestZone>();

        public static IReadOnlyList<QuestZone> ActiveZones => activeZones;

        /// <summary>호스트가 모든 활성 InProgress zone 에 적 처치 통지.</summary>
        public static void NotifyEnemyKilledToAllActive()
        {
            for (int i = activeZones.Count - 1; i >= 0; i--)
            {
                var z = activeZones[i];
                if (z != null) z.NotifyTargetKilled();
            }
        }

        // ===== 상태 (모든 클라 공유, 호스트가 RPC 로 갱신) =====
        private QuestState state = QuestState.Idle;
        private float waitElapsed;
        private float progressElapsed;
        private int killCount;
        private int[] spawnedBarrierIds;

        // 시각 마커 (런타임 LineRenderer)
        private LineRenderer rangeIndicator;

        public QuestState CurrentState => state;
        public QuestData Data => data;

        /// <summary>Waiting 상태에서 경과 시간(초). UI 카운트다운 표시용.</summary>
        public float WaitElapsed => waitElapsed;

        /// <summary>Waiting 시작까지 남은 시간(초). UI 카운트다운 표시용.</summary>
        public float WaitRemaining =>
            data != null ? Mathf.Max(0f, data.waitTime - waitElapsed) : 0f;

        /// <summary>InProgress timeLimit 가 있는 경우 남은 시간(초). 0 이면 무제한.</summary>
        public float TimeRemaining =>
            (data != null && data.timeLimit > 0f)
                ? Mathf.Max(0f, data.timeLimit - progressElapsed)
                : 0f;

        public int KillCount => killCount;

        // 진행률 (UI 바인딩용). KillTarget: kill/target. DodgeFalling: dodged/target. 그 외 0.
        public float ProgressRatio
        {
            get
            {
                if (data == null || data.targetCount <= 0) return 0f;
                if (state != QuestState.InProgress) return 0f;
                return Mathf.Clamp01((float)killCount / data.targetCount);
            }
        }

        private void Update()
        {
            if (data == null) return;
            if (!PhotonNetwork.IsMasterClient) return;
            if (state == QuestState.Completed || state == QuestState.Failed) return;

            // 게임 일시정지 / 메뉴 / 결과 화면 중에는 진행 정지.
            // 레벨업 패널 진입 시 GameManager.GameState=Paused 로 전환되므로 이 가드로 카운트 멈춤.
            var gm = SwDreams.Shared.Managers.GameManager.Instance;
            if (gm != null &&
                gm.CurrentState != SwDreams.Shared.Managers.GameManager.GameState.Playing &&
                gm.CurrentState != SwDreams.Shared.Managers.GameManager.GameState.BossFight)
                return;

            switch (state)
            {
                case QuestState.Idle:
                    TickIdle();
                    break;
                case QuestState.Waiting:
                    TickWaiting();
                    break;
                case QuestState.InProgress:
                    TickInProgress();
                    break;
            }
        }

        private void TickIdle()
        {
            if (!AreAllPlayersInside()) return;
            TransitionTo(QuestState.Waiting);
        }

        private void TickWaiting()
        {
            if (!AreAllPlayersInside())
            {
                // 한 명이라도 이탈 → Idle 리셋
                TransitionTo(QuestState.Idle);
                return;
            }

            waitElapsed += Time.deltaTime;
            if (waitElapsed >= data.waitTime)
                TransitionTo(QuestState.InProgress);
        }

        private void TickInProgress()
        {
            // 시간 제한
            if (data.timeLimit > 0f)
            {
                progressElapsed += Time.deltaTime;
                if (progressElapsed >= data.timeLimit)
                {
                    // KillInTime 미달성 → Failed. KillTarget 은 무제한이므로 timeLimit=0 권장.
                    if (data.questType == QuestType.KillInTime || data.questType == QuestType.Defend)
                    {
                        TransitionTo(QuestState.Failed);
                        return;
                    }
                }
            }

            // KillTarget / KillInTime: killCount 비교. KillCount 외부 갱신 (OnTargetKilled).
            if (data.questType == QuestType.KillTarget || data.questType == QuestType.KillInTime)
            {
                if (killCount >= data.targetCount)
                    TransitionTo(QuestState.Completed);
            }

            // DodgeFalling/Defend MVP 외 — TODO: 별도 핸들러 등록 시 처리.
        }

        // ===== 외부 진행 갱신 — 호스트만 =====

        /// <summary>퀘스트 대상 적이 처치됐을 때 호출. 호스트 측 SpawnManager 또는 Enemy.OnDeath 에서.</summary>
        public void NotifyTargetKilled()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (state != QuestState.InProgress) return;

            // 레벨업 패널 등 일시정지 중에는 잔여 투사체로 적이 죽어도 카운트 제외.
            var gm = SwDreams.Shared.Managers.GameManager.Instance;
            if (gm != null &&
                gm.CurrentState != SwDreams.Shared.Managers.GameManager.GameState.Playing &&
                gm.CurrentState != SwDreams.Shared.Managers.GameManager.GameState.BossFight)
                return;

            killCount++;
            // 진행률 갱신 RPC (UI 동기화용 — MVP 단계는 호스트만 카운트, 클라 UI 는 나중에).
        }

        // ===== 진입 판정 =====

        private static readonly List<GameObject> tmpPlayers = new List<GameObject>(8);

        private bool AreAllPlayersInside()
        {
            tmpPlayers.Clear();
            var found = GameObject.FindGameObjectsWithTag("Player");
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] == null) continue;
                tmpPlayers.Add(found[i]);
            }
            if (tmpPlayers.Count == 0) return false;

            float r2 = data.triggerRadius * data.triggerRadius;
            for (int i = 0; i < tmpPlayers.Count; i++)
            {
                var p = tmpPlayers[i];
                if (p == null) continue;
                Vector2 delta = (Vector2)(p.transform.position - transform.position);
                if (delta.sqrMagnitude > r2) return false;
            }
            return true;
        }

        // ===== 상태 전환 (호스트) + RPC =====

        private void TransitionTo(QuestState next)
        {
            if (state == next) return;
            state = next;

            switch (next)
            {
                case QuestState.Idle:
                    waitElapsed = 0f;
                    progressElapsed = 0f;
                    killCount = 0;
                    break;
                case QuestState.Waiting:
                    waitElapsed = 0f;
                    break;
                case QuestState.InProgress:
                    progressElapsed = 0f;
                    killCount = 0;
                    SpawnBarrierEnemies();
                    HideRangeIndicator();
                    if (!activeZones.Contains(this)) activeZones.Add(this);
                    break;
                case QuestState.Completed:
                    activeZones.Remove(this);
                    DespawnBarrierEnemies();
                    HideRangeIndicator();
                    DispatchReward();
                    break;
                case QuestState.Failed:
                    activeZones.Remove(this);
                    DespawnBarrierEnemies();
                    HideRangeIndicator();
                    break;
            }

            // OthersBuffered: 중도 참가 클라가 현재 state 를 버퍼에서 자동 수신.
            // QuestZone 은 Scene PhotonView 라 호스트 마이그레이션 후에도 새 호스트가 송신권 유지
            // (인스펙터에서 Owner=null/Master 로 셋업할 것).
            photonView.RPC(nameof(RPC_SyncState), RpcTarget.OthersBuffered, (int)next);
            Debug.Log($"[QuestZone] {data.displayName} → {next}");
        }

        [PunRPC]
        private void RPC_SyncState(int stateInt)
        {
            state = (QuestState)stateInt;
            // 클라 측 시각 마커 갱신 — 호스트에서 TransitionTo 가 호출하는 HideRangeIndicator 와 같은 동작을 클라에서도 재현.
            if (state == QuestState.InProgress
                || state == QuestState.Completed
                || state == QuestState.Failed)
            {
                HideRangeIndicator();
            }
        }

        private void Start()
        {
            CreateRangeIndicator();
        }

        private void OnDisable()
        {
            // 풀링/씬 종료 시 잔여 등록 제거.
            activeZones.Remove(this);
        }

        // ===== 시각 마커 (트리거 반경 흰색 원) =====

        private void CreateRangeIndicator()
        {
            if (data == null) return;
            if (rangeIndicator != null) return;

            var go = new GameObject("RangeIndicator");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;

            rangeIndicator = go.AddComponent<LineRenderer>();
            rangeIndicator.useWorldSpace = false;
            rangeIndicator.loop = true;
            rangeIndicator.positionCount = 64;
            rangeIndicator.startWidth = 0.05f;
            rangeIndicator.endWidth = 0.05f;
            rangeIndicator.startColor = Color.white;
            rangeIndicator.endColor = Color.white;
            rangeIndicator.material = new Material(Shader.Find("Sprites/Default"));
            rangeIndicator.sortingOrder = 0;

            int segments = rangeIndicator.positionCount;
            float r = data.triggerRadius;
            for (int i = 0; i < segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                rangeIndicator.SetPosition(i, new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0f));
            }
        }

        /// <summary>퀘스트 종료 시 거점 마커 비활성화 (Completed/Failed 공통).</summary>
        private void HideRangeIndicator()
        {
            if (rangeIndicator != null)
                rangeIndicator.enabled = false;
        }

        // ===== 격리 몹 — SpawnManager.SpawnQuestBarriers 경유 =====

        private void SpawnBarrierEnemies()
        {
            if (data.barrierEnemyData == null) return;
            if (data.barrierEnemyCount <= 0) return;

            var sm = SpawnManager.Instance;
            if (sm == null) return;

            spawnedBarrierIds = sm.SpawnQuestBarriers(
                data.barrierEnemyData,
                transform.position,
                data.barrierRadius,
                data.barrierEnemyCount);
        }

        private void DespawnBarrierEnemies()
        {
            if (spawnedBarrierIds == null) return;
            SpawnManager.Instance?.DespawnEnemies(spawnedBarrierIds);
            spawnedBarrierIds = null;
        }

        // ===== 보상 =====

        private void DispatchReward()
        {
            // QuestRewardDispatcher 가 LevelUpManager 경유로 StatBoost 선택지 트리거.
            QuestRewardDispatcher.DispatchStatBoostReward(data);
        }

        // ===== 디버그 =====

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;
            if (data == null) return;
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, data.triggerRadius);

            if (data.barrierEnemyData != null)
            {
                Gizmos.color = new Color(1f, 0.4f, 0.4f, 0.3f);
                Gizmos.DrawWireSphere(transform.position, data.barrierRadius);
            }
        }
    }
}
