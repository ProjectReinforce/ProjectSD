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

| 매니저 | 생명주기 | 이유 |
|---|---|---|
| **NetworkManager** | DontDestroyOnLoad | 모든 씬에서 Photon 연결 유지 |
| **AudioManager** | DontDestroyOnLoad | BGM 끊기지 않게 |
| **SceneController** | DontDestroyOnLoad | 씬 전환 관리 |
| **GameManager** | GameScene 만 | 게임 로직은 플레이 중에만 |
| **UIManager** | 각 씬마다 | 씬마다 UI 다름 |
| **SpawnManager** | GameScene 만 | 적 스폰은 플레이 중에만 |
| **SkillManager** | GameScene 만 | 스킬 상태는 게임 중에만 |
| **PoolManager** | GameScene 만 | 오브젝트 풀링 |
| **DamageCalculator** | GameScene 만 (정적/매니저 어느 쪽이든) | 호스트 판정 |
| **MenuSceneManager** | MenuScene 만 | 패널 전환 |

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

## 6. SceneController

**역할:** 씬 전환 관리 (DontDestroyOnLoad).

```csharp
public class SceneController : MonoBehaviour
{
    public static SceneController Instance;

    public void LoadGameScene()
    {
        if (PhotonNetwork.IsMasterClient)
            PhotonNetwork.LoadLevel("GameScene");
    }

    public void ReturnToWaitingRoom()
    {
        PhotonNetwork.CurrentRoom.SetCustomProperties(
            new Hashtable { { "returnToWaitingRoom", true } });
        PhotonNetwork.LoadLevel("MenuScene");
    }
}
```

씬 구조 상세는 [scene-structure.md](scene-structure.md).

## 7. MenuSceneManager

**역할:** MenuScene 내부 패널 전환 (TitlePanel / RoomListPanel / WaitingRoomPanel).

```csharp
void Start()
{
    if (PhotonNetwork.InRoom)
    {
        var props = PhotonNetwork.CurrentRoom.CustomProperties;
        if (props.ContainsKey("returnToWaitingRoom"))
        {
            ShowWaitingRoom();
            // 플래그 제거
            return;
        }
    }
    ShowTitle();
}
```

관련 컴포넌트: `TitlePanelController`, `RoomListPanelController`, `WaitingRoomPanelController`, `CharacterSelectUI`.

## 8. UIManager

씬마다 독립. 해당 씬의 UI 패널(`HUD`, `LevelUpPanel`, `MessagePanel`, `ResultPanel` 등) 관리.

**패턴:**
- `Awake`: 싱글톤 할당 (DontDestroyOnLoad 없음)
- `OnDestroy`: Instance = null

UI 프레임 시스템은 [ui-frame.md](ui-frame.md) 참조.

## 9. SpawnManager

**역할:** 적 스폰 (GameScene 에서만, 호스트만 실행).

- 시간대별 스폰 간격/동시 적 수 테이블 적용 (예정, [spawn-rules.md](spawn-rules.md) TBD).
- 스폰 위치: 맵 경계 랜덤 (플레이어 밀집 지역 회피).
- 인원수 스케일링 반영.

## 10. SkillManager

**역할:** 플레이어별 스킬 슬롯 관리 (GameScene, 각 클라이언트).

- 6슬롯 제한 ([../game-design/rules.md § 1](../game-design/rules.md))
- 레벨업 선택지 적용
- 진화 조합 감지·처리
- `applicableStats` 필터 주입은 Executor 경유 ([skill-executor.md § 3](skill-executor.md))

## 11. PoolManager

**역할:** 오브젝트 풀링 공용.

- 적, 투사체, 이펙트, 장판, 경험치 오브 등.
- 최대 풀 크기는 타입별 SO 설정 권장.

## 12. AudioManager

**역할:** BGM/SFX 재생 (DontDestroyOnLoad).

- DOTween 크로스페이드로 BGM 전환.
- SFX는 `PlayOneShot`.

## 13. 관리자 상호 참조

- GameManager → Network/UI/Spawn/Skill/Damage 에 요청.
- NetworkManager → UIManager 에 RPC 수신 결과 전달.
- SceneController → Photon 씬 전환 + CustomProperties 관리.

## 14. 기존 코드 참조

- `Assets/Scripts/Adapter/Manager/` — 각 매니저 구현 (신규 추가 시)
- `Assets/Scripts/Adapter/Network/NetworkManager.cs`
- `Assets/Scripts/Adapter/UI/UImanager.cs`, `MenuSceneManager.cs`

## 15. 알려진 제약

- [ ] 호스트 마이그레이션 시 SpawnManager 의 상태(이미 스폰된 적 리스트) 이관 정책 확정 필요
- [ ] 씬 전환 중 다이렉트로 호출받은 매니저의 null 안전성 패턴 확정
