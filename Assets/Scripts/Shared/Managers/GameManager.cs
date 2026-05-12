using System;
using SwDreams.Features.Progression.Application;
using SwDreams.Features.Progression.Domain.Formulas;
using SwDreams.Features.Character.Adapter.Data;
using SwDreams.Features.Character.Adapter;
using SwDreams.Features.Essence.Adapter.Data;
using SwDreams.Features.StatBoost.Adapter.Data;
using SwDreams.Features.Voice.Adapter.Data;
using SwDreams.Features.Weapon.Adapter.Data;
using UnityEngine;
using Photon.Pun;
using SwDreams.Shared.Managers;
using SwDreams.Shared.Data;
using SwDreams.Shared.Domain.Events;

namespace SwDreams.Shared.Managers
{
    /// <summary>
    /// 게임 상태 + 경험치/레벨업 관리.
    /// 호스트가 경험치 계산 후 RPC로 전체 클라이언트에 동기화.
    ///
    /// [CHANGED] GameplayConfig SO를 소유.
    /// 다른 매니저/엔티티는 GameManager.Instance.Config로 접근.
    /// → Inspector 연결이 GameManager 한 곳에만 필요.
    ///
    /// GameScene에 빈 GameObject → GameManager + PhotonView 부착.
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class GameManager : MonoBehaviourPun
    {
        public static GameManager Instance { get; private set; }

        public enum GameState
        {
            Loading,
            Playing,
            Paused,
            BossFight,
            GameClear,
            GameOver
        }

        // [CHANGED] 게임플레이 설정 SO. Inspector에서 연결.
        // 모든 매니저/엔티티가 GameManager.Instance.Config로 접근.
        [Header("게임 설정")]
        [SerializeField] private GameplayConfig config;

        // Phase 7: 캐릭터 데이터베이스. Inspector에서 연결.
        [SerializeField] private CharacterDatabase characterDatabase;

        // 정수 데이터베이스. Inspector에서 연결. DropSpawner/PlayerEssenceInventory 공용 SSOT.
        [SerializeField] private EssenceDatabase essenceDatabase;

        // 무기 데이터베이스. Inspector에서 연결. DropSpawner/PlayerWeaponInventory 공용 SSOT.
        [SerializeField] private WeaponDatabase weaponDatabase;

        // 능력치 부스트 DB. LevelUpManager 만렙 분기 + 퀘스트 보상에서 공용 사용.
        [SerializeField] private StatBoostDatabase statBoostDatabase;

        // R3 마이크 필터 DB. DropSpawner 가 픽업 결정 시 / MicFilterController 가 인덱스 해결 시 사용.
        [SerializeField] private MicFilterDatabase micFilterDatabase;

        // 메타 언락 카탈로그 (RefreshCharge 마일스톤 등 SO 없는 보상). UnlockTracker 가 평가.
        // 비어있으면 RefreshCharge 보너스 = 0 (스킬/무기/캐릭터 unlockConditions 평가는 별도로 동작).
        [SerializeField] private SwDreams.Features.Unlock.Adapter.Data.UnlockCatalog unlockCatalog;

        /// <summary>
        /// 게임플레이 설정 SO. 읽기 전용 접근.
        /// null 체크 후 사용 권장:
        ///   var cfg = GameManager.Instance?.Config;
        ///   if (cfg != null) { ... }
        /// </summary>
        public GameplayConfig Config => config;

        /// <summary>
        /// 캐릭터 데이터베이스. PlayerStub 초기화 시 사용.
        /// </summary>
        public CharacterDatabase CharacterDB => characterDatabase;

        /// <summary>
        /// 정수 데이터베이스. DropSpawner (스폰 시 속성별 시각/효과 조회),
        /// PlayerEssenceInventory (장착 시 injectedEffects 주입) 공용 진입점.
        /// </summary>
        public EssenceDatabase EssenceDB => essenceDatabase;

        /// <summary>
        /// 무기 데이터베이스. DropSpawner (등급별 무기 샘플링),
        /// PlayerWeaponInventory (조합 결과 해결) 공용 진입점.
        /// </summary>
        public WeaponDatabase WeaponDB => weaponDatabase;

        /// <summary>
        /// 능력치 부스트 DB. LevelUpManager 가 스킬 풀 고갈(만렙) 시 / 퀘스트 보상에서 선택지 생성용.
        /// </summary>
        public StatBoostDatabase StatBoostDB => statBoostDatabase;

        /// <summary>
        /// R3 마이크 필터 DB. MicFilterPickup 이 호스트에서 인덱스 롤 + 호스트→클라 RPC 인자로 인덱스 전송,
        /// MicFilterController 가 인덱스 → MicFilterData 해결.
        /// </summary>
        public MicFilterDatabase MicFilterDB => micFilterDatabase;

        /// <summary>
        /// 메타 언락 카탈로그 (UnlockTracker 가 평가). null 가능 — 그 경우 RefreshCharge 마일스톤 0.
        /// </summary>
        public SwDreams.Features.Unlock.Adapter.Data.UnlockCatalog UnlockCatalog => unlockCatalog;

        // Application 서비스
        private ExperienceService expService = new ExperienceService();

        // 상태
        public GameState CurrentState { get; private set; } = GameState.Loading;
        public float GameTime { get; private set; }

        // 경험치/레벨: 클라이언트 동기화용 필드 (RPC_SyncExp에서 갱신)
        private int syncedLevel = 1;
        private int syncedExp = 0;
        public int TeamLevel => syncedLevel;
        public int TeamExp => syncedExp;
        public int TeamRequiredExp => LevelTable.GetRequiredExp(syncedLevel);

        // 이벤트
        public event Action<int, int> OnExpChanged;     // current, required
        public event Action<int> OnLevelUp;             // newLevel
        public event Action<GameState> OnStateChanged;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
            {
                Destroy(gameObject);
                return;
            }

            // [CHANGED] Config 누락 경고
            if (config == null)
                Debug.LogWarning("[GameManager] GameplayConfig SO가 연결되지 않았습니다. " +
                                 "Inspector에서 연결하세요. 각 시스템이 기본값으로 동작합니다.");

            // B-1a: 인-런 통계 누적기 인스턴스 보장 (run-statistics.md §5).
            // 게임 시작 직후 첫 데미지/킬 RPC 가 도착해도 안전하게 카운트되도록.
            SwDreams.Features.Stats.Adapter.LocalStatsRecorder.GetOrCreate();

            // 메타 진행도 영구 누적 인스턴스 보장 (meta-unlock.md §11).
            // RunEventBus 를 통해 LocalStatsRecorder 의 OnKill/OnBossDefeat/OnDeath 와
            // GameManager 의 RunEnded 발화를 자기 PC PlayerPrefs 에 누적.
            // 등록 순서: MetaProgressStore 가 먼저 (totalRuns/totalClears 갱신) → UnlockTracker 가
            // 그 후 평가 (RunEnded 멀티캐스트 등록 순서대로).
            SwDreams.Features.Unlock.Adapter.MetaProgressStore.GetOrCreate();
            SwDreams.Features.Unlock.Adapter.UnlockTracker.GetOrCreate();

            // 멀티플레이 권위 모델 (D5) — 자기 unlocked 셋을 Photon CustomProperties 로 공유.
            // OnJoinedRoom 에 자동 push (이미 룸이면 OnEnable 에서 push).
            SwDreams.Features.Unlock.Adapter.UnlockSetSync.GetOrCreate();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (CurrentState == GameState.Playing || CurrentState == GameState.BossFight)
                GameTime += Time.deltaTime;
        }

        /// <summary>
        /// 경험치 추가. 호스트에서만 호출.
        /// 계산 후 RPC로 전체 클라이언트에 동기화.
        /// </summary>
        public void AddExp(int amount)
        {
            if (!PhotonNetwork.IsMasterClient) return;

            int leveledUp = expService.AddExp(amount);

            photonView.RPC(nameof(RPC_SyncExp), RpcTarget.All,
                expService.CurrentExp, expService.GetRequiredExp(), expService.CurrentLevel, leveledUp);
        }

        [PunRPC]
        private void RPC_SyncExp(int currentExp, int requiredExp, int level, int levelUpCount)
        {
            // 클라이언트 동기화 필드 갱신
            syncedLevel = level;
            syncedExp = currentExp;

            OnExpChanged?.Invoke(currentExp, requiredExp);

            // 레벨업 횟수만큼 이벤트 발행 → LevelUpManager가 큐에 쌓음
            for (int i = 0; i < levelUpCount; i++)
            {
                int lvl = level - levelUpCount + i + 1; // 중간 레벨도 정확히 전달
                Debug.Log($"[GameManager] 레벨업! Lv.{lvl}");
                OnLevelUp?.Invoke(lvl);
            }
        }

        public void ChangeState(GameState newState)
        {
            CurrentState = newState;
            OnStateChanged?.Invoke(newState);
            EmitRunEndedIfTerminal(newState);
        }

        // 같은 런에서 GameClear/GameOver 가 두 번 진입(ChangeState + RPC_ChangeState 등)해도
        // RaiseRunEnded 가 1회만 발화되도록 throttle. 비종료 state 진입 시 자동 리셋.
        private bool runEndedEmitted;

        private void EmitRunEndedIfTerminal(GameState s)
        {
            bool terminal = (s == GameState.GameClear || s == GameState.GameOver);
            if (!terminal)
            {
                runEndedEmitted = false;
                return;
            }
            if (runEndedEmitted) return;
            runEndedEmitted = true;
            RunEventBus.Instance.RaiseRunEnded(s == GameState.GameClear);
        }

        /// <summary>
        /// ESC 인게임 메뉴가 솔로 정지 시 set 하는 보조 플래그.
        /// GameState=Paused 만으로는 LevelUpManager 자기참조 문제(자기가 만든 Paused 에 자기가 묶임)
        /// 때문에 LevelUp 타이머가 안 멈춤. 이 플래그로 외부 정지원을 구분한다.
        ///
        /// 솔로 한정 (멀티는 set 안 함 — ESC 메뉴 정책상 게임 흐름 유지).
        /// LevelUpManager 등 시간 기반 시스템이 가드용으로 본다.
        /// </summary>
        public bool IsMenuPaused { get; private set; }

        public void SetMenuPaused(bool paused)
        {
            IsMenuPaused = paused;
        }

        /// <summary>
        /// 네트워크 상태 전환. 호스트에서만 호출.
        /// 모든 클라이언트에서 동시에 상태 변경.
        /// </summary>
        public void ChangeStateNetwork(GameState newState)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            photonView.RPC(nameof(RPC_ChangeState), RpcTarget.All, (int)newState);
        }

        [PunRPC]
        private void RPC_ChangeState(int stateInt)
        {
            GameState newState = (GameState)stateInt;
            CurrentState = newState;
            OnStateChanged?.Invoke(newState);
            Debug.Log($"[GameManager] 상태 전환(Network): {newState}");
            EmitRunEndedIfTerminal(newState);
        }
    }
}