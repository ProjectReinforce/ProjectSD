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
        }
    }
}