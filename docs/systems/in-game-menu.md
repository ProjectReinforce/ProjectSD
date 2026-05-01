# 시스템 명세서: 인게임 ESC 메뉴

## 1. 메타

| 항목 | 값 |
|---|---|
| 시스템 ID | `in-game-menu` |
| 이름 | 인게임 ESC 메뉴 시스템 |
| 분류 | UI / 입력 / 네트워크 |
| 의존 레이어 | Adapter (입력/상태 분기), Presentation (모달 UI) |
| 구현 상태 | ⬜ 설계만 |
| 최종 업데이트 | 2026-05-01 |

## 2. 목적

게임 중 ESC 입력으로 즉시 호출되는 메뉴를 제공한다. 기능은 **게임 재개 / 설정 / 룸 나가기 / 게임 종료**.

해결하는 문제:
- 게임 중 빠른 룸 이탈 / 게임 종료 / 설정 변경 진입점 부재
- 멀티플레이에서 진정한 일시정지가 불가능한 한계 — 솔로(1명) 한정 정지 + 멀티 로컬 UI 토글의 이원화 정책 명시
- 다른 인게임 UI(레벨업·결과창·HUD)와의 우선순위 충돌 룰 표준화

## 3. 핵심 정책 (결정)

| 항목 | 정책 | 비고 |
|---|---|---|
| **UI 형태** | 반투명 dim 배경 + **중앙 모달 카드** | 사이드/중앙 모두 시야 가림은 동일 → 캐릭터가 화면 중앙이라 중앙 모달이 자연스러움 |
| **솔로 정지 (PlayerCount == 1)** | ✅ `GameState.Paused` 로 진정 정지 | 닫을 때 Playing 또는 BossFight 로 이전 상태 복원 |
| **멀티 정지 (PlayerCount >= 2)** | ❌ GameState 안 건드림 | 로컬 UI 토글만. 게임 시간/AI/스폰/타이머 모두 그대로 흐름. 다른 플레이어 페널티 + 악용 방지 |
| **중도 입장** | ❌ 게임 룰상 없음 | "정지 중 외부 입장자" 케이스 처리 불필요 |
| **메뉴 항목** | Resume / 설정 / 룸 나가기 / 게임 종료 | 4개 고정 |
| **설정 UI** | 메뉴 씬의 기존 설정 패널 재활용 | 중복 작성 방지. 재활용 방식은 구현 단계 결정 (§ 9) |
| **호출 가능 상태** | **열기**: `Playing` / `BossFight` / `Paused`(레벨업 중) — 그 외(`Loading`/`GameClear`/`GameOver`)는 ESC 무시. **닫기**: 이미 열려있으면 GameState 무관하게 항상 허용 | 열린 채 GameOver 진입해도 ESC 로 닫고 결과창 접근 가능 |
| **다른 UI 와 우선순위** | ESC 메뉴가 **최상단** | 레벨업 패널 떠 있어도 그 위에 표시 |
| **레벨업 중 + ESC** | ESC 메뉴를 레벨업 패널 위에 띄움. **솔로**: LevelUp 타이머도 정지 (`GameManager.IsMenuPaused` 가드). **멀티**: 타이머 그대로 흐름 | ESC 떠있는 동안 시간 놓치는 건 사용자 책임(멀티 한정) |
| **룸 나가기 행선지** | 메뉴 씬 → **룸 리스트 패널** | 호스트는 leave 시 마이그레이션, 게스트는 단순 leave (양쪽 동일 행선지) |
| **게임 종료** | `Application.Quit()` | 에디터에선 `EditorApplication.isPlaying = false` 로 분기 |

## 4. UI 구조

```
GameScene/
├── InGameMenuCanvas (Canvas, sortOrder = 100)  ← 인게임 UI 중 최상단
│   ├── DimBackground (Image, 반투명 검정 0.5)
│   └── MenuCard (Vertical Layout, 화면 중앙)
│       ├── Title ("일시정지" / "메뉴")
│       ├── Btn_Resume
│       ├── Btn_Settings    ← 메뉴 씬 설정 패널 재활용
│       ├── Btn_LeaveRoom
│       └── Btn_QuitGame
└── (확인 다이얼로그용 FrameToast 또는 별도 모달 — 룸 나가기/게임 종료 확인용)
```

**캔버스 sortOrder 표준 (인게임)**

| 캔버스 | sortOrder |
|---|---|
| InGameHUD | 10 |
| LevelUpPanel | 50 |
| ResultPanel | 80 |
| **InGameMenuCanvas** | **100** |
| ReconnectOverlay | 200 (강제 모달, ESC 메뉴보다도 위) |

> ESC 메뉴가 뜬 상태에서 ReconnectOverlay 발동 시(연결 끊김) 재접속 UI가 우선. 정상.

## 5. 인터페이스

```csharp
public class InGameMenuController : MonoBehaviour
{
    public bool IsOpen { get; }
    public void Open();
    public void Close();
    public void Toggle();
}
```

ESC 입력 처리:
- `Update()` 에서 `Keyboard.current.escapeKey.wasPressedThisFrame` 감지
- **이미 열려있으면 GameState 무관하게 닫기** (GameOver/GameClear 진입 후에도 결과창 접근 가능하도록)
- 닫혀있을 때 새로 열기는 호출 가능 상태(`Playing` / `BossFight` / `Paused`) 한정. 그 외(`Loading` / `GameClear` / `GameOver`)는 ESC 무시

## 6. 정지 분기 로직

```
[ESC 누름]
  if !IsOpen:
    cachedPrevState = GameManager.CurrentState
    // 솔로(1명) + 현재 Playing/BossFight 일 때만 정지 발동.
    // 이미 Paused(레벨업 중)면 그대로 둔다 — 레벨업 끝나면 LevelUpManager 가 복원하므로 간섭 X.
    if PlayerCount == 1 && (cachedPrevState == Playing || cachedPrevState == BossFight):
      GameManager.SetState(Paused)
    Open()
  else:
    Close()
    // ESC 누를 때 Playing/BossFight 였던 케이스만 복원.
    // Paused 였던(레벨업) 케이스는 LevelUpManager 권한이라 건드리지 않음.
    if PlayerCount == 1 && (cachedPrevState == Playing || cachedPrevState == BossFight):
      GameManager.SetState(cachedPrevState)
```

**주의:**
- 보스전 중 ESC → Paused → 닫을 때 BossFight 로 복원. Playing 으로 잘못 복원하면 보스 페이즈 매니저 쪽 버그 가능. 직전 상태 캐싱 필수.
- **레벨업 중 ESC** 케이스는 GameState 안 건드린다 — 이미 Paused 이고, 그 상태 권한은 LevelUpManager 에 있음. ESC 메뉴는 UI 만 띄움.

**보조 정지 플래그 — `GameManager.IsMenuPaused`:**
GameState=Paused 만으로는 LevelUpManager 의 자기참조 문제(자기가 만든 Paused 에 자기가 묶임 → `Update` 에서 무시) 때문에 LevelUp 타이머가 안 멈춤. 이를 해결하기 위해 별도 플래그를 둔다.

```
[ESC 누름] (Open)
  if 솔로:
    GameManager.SetMenuPaused(true)   // didMenuPause=true 캐싱
    if cachedPrevState in (Playing, BossFight):
      GameManager.ChangeState(Paused)  // 적/투사체/스폰 정지
[ESC 다시 누름] (Close)
  if didMenuPause: GameManager.SetMenuPaused(false)
  if didPauseGame: GameManager.ChangeState(cachedPrevState)
```

- `IsMenuPaused` 는 **솔로 한정** — 멀티는 set 안 함 (게임 흐름 유지 정책).
- `LevelUpManager.Update` 가 이 플래그를 가드로 사용 → 솔로 + 레벨업 중 ESC 시 타이머 정지 ✅
- 향후 다른 시간 기반 시스템(보스 페이즈 타이머 등)도 동일 가드 패턴 사용 가능.

## 7. 메뉴 항목별 동작

### Resume
- `Close()` 호출. § 6 분기에 따라 솔로면 GameState 복원.

### 설정
- 메뉴 씬의 설정 패널을 GameScene 위에 띄움.
- **재활용 방식 (구현 단계 결정):**
  - A. 같은 프리팹을 GameScene 캔버스에 인스턴스화
  - B. 메뉴 씬 설정 UI 를 별도 프리팹으로 분리 후 양쪽 씬에서 참조 (= "공통 설정 프리팹")
  - **B 가 가장 깔끔** 하지만 메뉴 씬 설정 패널 현재 구조 보고 결정. A 도 무방 (단순 인스턴스화).

### 룸 나가기
1. 확인 다이얼로그 ("정말 나가시겠습니까?")
2. 확인 시:
   - `PhotonNetwork.LeaveRoom()`
   - 호스트면 [HostMigrationHandler.cs](../../Assets/Scripts/Shared/Managers/HostMigrationHandler.cs) 가 마이그레이션 자동 처리 (기존 인프라)
   - `OnLeftRoom` 콜백에서 메뉴 씬 로드 → 룸 리스트 패널 진입
3. 행선지: **룸 리스트 패널**

### 게임 종료
1. 확인 다이얼로그
2. 확인 시:
   ```csharp
   #if UNITY_EDITOR
     UnityEditor.EditorApplication.isPlaying = false;
   #else
     Application.Quit();
   #endif
   ```

## 8. 네트워크

네트워크 기본 규약은 [network-sync.md](network-sync.md) 참조.

- **GameState 변경:** 솔로(PlayerCount == 1) 한정 **로컬 적용**. RPC 동기화 X (혼자라 의미 없음).
- **로컬 UI 토글:** 네트워크 X. 각 클라가 독립적으로 ESC 메뉴 열고 닫음.
- **룸 나가기:** Photon 표준 `LeaveRoom()` API + 기존 [HostMigrationHandler](../../Assets/Scripts/Shared/Managers/HostMigrationHandler.cs).
- **레벨업/결과창과 충돌:** 레벨업 RPC, 결과창 RPC 가 ESC 메뉴 떠있는 동안 도달해도 sortOrder 가 더 높아 그대로 표시. 별도 처리 X.

## 9. 알려진 제약 / 트레이드오프

- [x] **멀티에서 게임 안 멈춤** — 의도된 정책. 멀티 정지는 다른 플레이어 페널티 + 악용 가능성
- [x] **레벨업 타이머와 ESC 동시 (멀티)** — 자동 선택 그대로 발동. ESC 메뉴 위로 레벨업 결과 노출
- [x] **솔로 정지 중 외부 입장자** — 게임 룰상 중도 입장 없음. N/A
- [x] **이전 상태 복원** — Playing/BossFight 중 캐싱 필수. 잘못 복원 시 보스 페이즈 버그 가능
- [ ] **설정 메뉴 재활용 방식** — 메뉴 씬 설정 패널 구조 보고 § 7 옵션 A/B 중 결정
- [ ] **호출 가능 상태 확장** — 향후 새 GameState 추가 시 ESC 무시/허용 룰 갱신 필요
- [ ] **게임패드 입력** — 현재 키보드 ESC 만 가정. 추후 게임패드 매핑 시 Start 버튼 등 추가
- [ ] **확인 다이얼로그 UI** — `FrameToast` 는 비모달이라 부적합. 모달 확인창은 `Frame_PopUp` 이 미작성 ([ui-frame.md](ui-frame.md)) — 이 시스템보다 먼저 또는 같이 진행 필요

## 10. 기존 코드 참조

- **상태 관리:** [Assets/Scripts/Shared/Managers/GameManager.cs](../../Assets/Scripts/Shared/Managers/GameManager.cs) — `GameState`, `CurrentState`, `SetState`
- **레벨업 패널 (충돌 케이스):** [Assets/Scripts/Features/Progression/Adapter/Levelupmanager.cs](../../Assets/Scripts/Features/Progression/Adapter/Levelupmanager.cs)
- **호스트 마이그레이션:** [Assets/Scripts/Shared/Managers/HostMigrationHandler.cs](../../Assets/Scripts/Shared/Managers/HostMigrationHandler.cs)
- **메뉴 씬 진입점:** [Assets/Scripts/Features/UI/Adapter/Menu/MenuSceneManager.cs](../../Assets/Scripts/Features/UI/Adapter/Menu/MenuSceneManager.cs), [RoomListPanelController.cs](../../Assets/Scripts/Features/UI/Adapter/Menu/RoomListPanelController.cs)
- **메뉴 씬 설정 UI 재활용 대상:** 구현 단계에서 정확한 경로 파악
- **관련 시스템:** [scene-structure.md](scene-structure.md), [network-sync.md](network-sync.md), [ui-frame.md](ui-frame.md)

## 11. 변경 이력

- 2026-05-01: 초안. UI 형태(중앙 모달) / 솔로 정지 정책 / 메뉴 항목 4개 / 룸 리스트 행선지 / 멀티 레벨업 타이머 흐름 유지 결정.
- 2026-05-01: 호출 가능 상태에 `Paused`(레벨업 중) 포함하도록 수정. 레벨업 패널 위에 ESC 메뉴 띄우는 결정 반영. 설정 메뉴 재활용 옵션에서 Additive 씬 로드 방식 제거.
- 2026-05-01: `GameManager.IsMenuPaused` 보조 정지 플래그 도입. 솔로 + 레벨업 중 ESC 에서도 LevelUp 타이머 정지하도록 정책 정정. `LevelUpManager.Update` 에 가드 추가.
- 2026-05-01: 닫기 ESC 는 GameState 무관하게 허용. 메뉴 떠있는 채로 GameOver/GameClear 진입 시 결과창 접근 불가 버그 수정 (열기/닫기 분기 — `CanOpenNow`).
