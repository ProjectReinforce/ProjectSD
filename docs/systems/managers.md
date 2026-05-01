# Managers — 싱글톤 매니저 레이어

Sweepin' Dreams 의 매니저 클래스 구조. 각 매니저의 생명주기·책임·상호 참조를 정리.

## 1. 메타

| 항목 | 값 |
|---|---|
| 시스템 ID | `managers` |
| 분류 | 아키텍처 / 전반 |
| 의존 레이어 | Adapter |
| 최종 업데이트 | 2026-04-18 |

## 2. 목적

씬 간 상태 유지(네트워크, 오디오), 씬별 게임 로직(스폰, UI, 전투) 을 책임지는 싱글톤 매니저 레이어 정의.

## 3. 매니저 목록과 생명주기

| 매니저 | 위치 | 생명주기 | 이유 |
|---|---|---|---|
| **NetworkManager** | `Shared/Managers/` | DontDestroyOnLoad | 모든 씬에서 Photon 연결 유지 |
| **AudioManager** | `Shared/Managers/` | DontDestroyOnLoad | BGM 끊기지 않게 |
| **GameManager** | `Shared/Managers/` | GameScene 만 | 게임 로직 + 팀 경험치/레벨 + GameState |
| **ResultManager** | `Shared/Managers/` | GameScene 만 | 결과 수집·RPC 브로드캐스트·씬 전환 |
| **SpawnManager** | `Shared/Managers/` | GameScene 만 | 적 스폰 (호스트만 판정) |
| **PoolManager** | `Shared/Managers/` | GameScene 만 | 오브젝트 풀링 |
| **GameStatTracker** | `Shared/Managers/` | GameScene 만 | 킬/데스 누적 (호스트만 기록) |
| **DifficultyManager** | `Shared/Managers/` | GameScene 만 | 난이도 곡선·시간대 스케일링 |
| **HostMigrationHandler** | `Shared/Managers/` | DontDestroyOnLoad | MasterClient 전환 시 비상 처리 |
| **GameAudioConnector** | `Shared/Managers/` | GameScene 만 | 게임 이벤트 → AudioManager 다리 |
| **SceneTransitionManager** | `Shared/Managers/` | DontDestroyOnLoad | 씬 전환 진입점 (`EnterGameSceneByMaster` / `ReturnToMenu` / `ReturnToWaitingRoom`) + 자체 `GameState` enum (`None/Menu/WaitingRoom/InGame/Result/Paused`). ※ `GameManager.GameState` 와 별개 (통합 검토 부채) |
| **UIManager (`UImanager`)** | `Features/UI/Presentation/` | 각 씬마다 | 씬마다 UI 다름. 파일명 소문자 m 주의 |
| **MenuSceneManager** | `Features/UI/Adapter/Menu/` | MenuScene 만 | 패널 전환 (Title/RoomList/WaitingRoom) |
| **Levelupmanager / SkillManager / ChaosSkillManager** | `Features/Progression/Adapter/` | GameScene 만 | 레벨업·스킬·혼돈 슬롯 관리. 파일명 표기 그대로 |
| **RespawnManager** | `Features/Character/Adapter/` | GameScene 만 | 부활 RPC + 카운트다운 |

### 싱글톤 패턴 원칙

- `Instance` 정적 프로퍼티 노출.
- DontDestroyOnLoad 매니저는 중복 인스턴스 감지 시 자기 `Destroy`.
- **씬 한정 매니저는 DontDestroyOnLoad 사용 금지** (씬 로드마다 새로 생성).

## 4. GameManager

**역할:** 게임 플로우 전체 관리 (GameScene 에서만 생성).

```csharp
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public enum GameState
    {
        Loading,    // 씬 로드 중
        Playing,    // 게임 진행 중
        Paused,     // 레벨업/메뉴
        BossFight,  // 보스전
        GameClear,
        GameOver
    }

    public GameState CurrentState { get; private set; }
    public int TeamLevel { get; private set; } = 1;
    public int TeamExp { get; private set; } = 0;
    public float GameTime { get; private set; } = 0f;
    public ChaosSkillData BossChaosSkill { get; private set; }

    public void AddExp(int exp) { /* 호스트 판정 → 레벨업 */ }
    public bool AllPlayersDead() { /* 전원 사망 판정 */ }
}
```

**상태 전이:** [../game-design/flow-design.md § 3](../game-design/flow-design.md) 의 상태 전이 테이블과 일치.

## 5. NetworkManager

**역할:** Photon 연결 및 RPC 중앙 처리 (DontDestroyOnLoad).

- `PhotonNetwork.AutomaticallySyncScene = true` 설정.
- 콜백 중앙 처리: `OnConnectedToMaster`, `OnJoinedRoom`, `OnDisconnected`, `OnMasterClientSwitched` 등.
- 주요 RPC 목록은 [network-sync.md § 5](network-sync.md).

## 6. 씬 전환 (전용 SceneController 없음)

별도의 `SceneController` 매니저는 **현재 존재하지 않는다.** 씬 전환은 다음 두 곳에서 직접 호출한다:

- **MenuScene → GameScene:** `WaitingRoomPanelController` 또는 `NetworkManager` 가 `PhotonNetwork.LoadLevel("GameScene")` 직접 호출. `AutomaticallySyncScene = true` 라 호스트만 호출하면 전체 전환.
- **GameScene → MenuScene (다시 하기/나가기):** `Shared/Managers/ResultManager.cs` 의 `OnRetry()` / `OnExit()` 가 처리. 다시 하기는 방을 유지한 채 `SceneManager.LoadScene("MenuScene")`, 나가기는 `LeaveRoom()` → 콜백에서 씬 전환.

씬 구조 상세는 [scene-structure.md](scene-structure.md).

## 7. MenuSceneManager

**역할:** MenuScene 내부 패널 전환 (`TitlePanel` / `RoomListPanel` / `WaitingRoomPanel`).

**복귀 로직:** 씬 간 전달은 **CustomProperties 가 아니라 `static bool ReturnToRoomList` 플래그**.

```csharp
// Features/UI/Adapter/Menu/MenuSceneManager.cs
public static bool ReturnToRoomList { get; set; }  // 한 번 읽으면 소비

private void Start()
{
    AudioManager.Instance?.PlayMenuBGM();

    // 방에 남아 돌아온 경우 (다시 하기) → 대기실
    if (PhotonNetwork.InRoom)
    {
        ShowWaitingRoom();
        return;
    }

    // 게임씬 나가기로 돌아온 경우 → 방 리스트
    if (ReturnToRoomList)
    {
        ReturnToRoomList = false;
        ShowRoomList();
        return;
    }

    // 그 외 → 타이틀
    ShowTitle();
}
```

> **WHY static 플래그:** 씬 전환 시 모든 MonoBehaviour 인스턴스가 파괴되므로 이전 씬에서 다음 씬으로 값을 전달하려면 static 또는 DontDestroyOnLoad 가 필요. 이 플래그는 MenuSceneManager 만 읽고 쓰므로 SRP 상 여기에 둔다.

관련 컴포넌트: `TitlePanelController`, `RoomListPanelController`, `WaitingRoomPanelController`, `CharacterSelectUI` (모두 `Features/UI/Adapter/Menu/`).

## 8. UIManager

씬마다 독립. 해당 씬의 UI 패널(`HUD`, `LevelUpPanel`, `MessagePanel`, `ResultPanel` 등) 관리.

**패턴:**
- `Awake`: 싱글톤 할당 (DontDestroyOnLoad 없음)
- `OnDestroy`: Instance = null

UI 프레임 시스템은 [ui-frame.md](ui-frame.md) 참조.

## 9. SpawnManager

**역할:** 적 스폰 (GameScene 에서만, 호스트만 판정).

- 시간대별 스폰 간격/동시 적 수 테이블 적용 (TBD, [spawn-rules.md](spawn-rules.md))
- 스폰 위치: 맵 경계 랜덤 (플레이어 밀집 지역 회피)
- 인원수 스케일링은 `DifficultyManager` 와 연동
- 데미지 요청 RPC 도 본 매니저가 수신: `RPC_RequestDamage`, `RPC_RequestKnockback` ([network-sync.md § 5-2](network-sync.md))

## 10. Levelupmanager / SkillManager / ChaosSkillManager

**위치:** `Features/Progression/Adapter/`. 파일명은 코드 상 표기 그대로.

- **Levelupmanager:** 레벨업 선택지 RPC (`RPC_ReceiveChoices`, `RPC_ForceChoice` 등 6종 — [network-sync.md § 5-5](network-sync.md))
- **SkillManager:** 플레이어별 스킬 슬롯 관리. 6슬롯 제한 ([../game-design/rules.md § 1](../game-design/rules.md)), 진화 조합 감지·처리
- **ChaosSkillManager:** 혼돈 스킬 슬롯 별도 관리
- `applicableStats` 필터 주입은 Executor 경유 ([skill-executor.md § 3](skill-executor.md))

## 11. PoolManager

**역할:** 오브젝트 풀링 공용. (`Shared/Managers/PoolManager.cs`)

- 적, 투사체, 이펙트, 장판, 경험치 오브 등.
- 최대 풀 크기는 타입별 SO 설정.

## 12. AudioManager + GameAudioConnector

**위치:** `Shared/Managers/`.

- **AudioManager:** BGM/SFX 재생. DontDestroyOnLoad.
- **GameAudioConnector:** GameScene 전용. 게임 이벤트(보스 등장, 레벨업 등) → AudioManager 호출 다리.

## 13. ResultManager + GameStatTracker + DifficultyManager + HostMigrationHandler

**위치:** `Shared/Managers/`.

- **ResultManager:** Run 종료 감지 → 빌드 데이터 수집 → 결과 RPC 브로드캐스트 → UI 표시 → 다시 하기/나가기 처리
- **GameStatTracker:** 호스트 전용. 킬·데스 누적 (결과 화면용)
- **DifficultyManager:** 시간대별 난이도 곡선
- **HostMigrationHandler:** MasterClient 전환 시 비상 처리. DontDestroyOnLoad

## 14. 관리자 상호 참조

- `GameManager` → `NetworkManager`(연결 상태) / `ResultManager`(종료 트리거) / 각 Feature 매니저(레벨업·스폰 등)에 신호
- `NetworkManager` → 콜백을 `Action`/`event` 로 노출, 다른 매니저가 구독
- `ResultManager` → `GameStatTracker`, `BossChaosApplicator`, `Player*` 컴포넌트에서 빌드 데이터 수집

## 15. 기존 코드 참조

- `Assets/Scripts/Shared/Managers/*.cs` — 공용 싱글턴 (Network/Game/Result/Spawn/Audio/Pool/GameStat/Difficulty/HostMigration/GameAudioConnector/SceneTransition)
- `Assets/Scripts/Shared/Network/NetworkAdapter.cs` — 트리거/데미지 통지 RPC
- `Assets/Scripts/Features/UI/Presentation/UImanager.cs` — 인게임 UI 매니저
- `Assets/Scripts/Features/UI/Adapter/Menu/MenuSceneManager.cs` — 메뉴 씬 패널 전환

## 15. 알려진 제약

- [ ] 호스트 마이그레이션 시 SpawnManager 의 상태(이미 스폰된 적 리스트) 이관 정책 확정 필요
- [ ] 씬 전환 중 다이렉트로 호출받은 매니저의 null 안전성 패턴 확정
