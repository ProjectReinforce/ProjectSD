# World Indicator — 파티원 / 보스 위치 표시 시스템

화면 안에 있는 동안은 머리 위 이름표만, 화면 밖으로 나가면 가장자리 인디케이터로 전환되는 위치 표시 시스템. 본 문서는 **다른 세션이 이 문서만 보고 구현을 끝낼 수 있도록** 작성되었다.

## 1. 메타

| 항목 | 값 |
|---|---|
| 시스템 ID | `world-indicator` |
| 분류 | UI |
| 의존 레이어 | Adapter (Manager, Interface, Policy) + Presentation (View) |
| 최종 업데이트 | 2026-04-26 |
| 구현 상태 | ✅ 구현 완료 (2026-04-26) — ledger [completed-work.md](../architecture/completed-work.md). 랜덤 퀘스트 인디케이터까지 흡수, Manager pending drain 패턴으로 Awake race 차단 |

## 2. 목적

**문제:** 4인 Co-op 환경에서 파티원이 화면 밖으로 나가면 위치 파악이 어려움. 보스도 페이즈 이동 패턴 후 화면 밖으로 잠시 빠지면 추적 불가.

**해결:** 화면 외부의 추적 대상 위치를 화면 가장자리 인디케이터로 표시. 화면 안에 있을 때는 머리 위 이름만 노출(잡음 최소화).

**범위 (R11):** 파티원 + 보스만 실 등록. 퀘스트는 인터페이스만 깔고 보류 — 고정 위치 퀘스트가 다수일 가능성이 있어 표시 정책을 별도 결정.

## 3. 표시 정책 (카테고리별)

| 대상 | `IndicatorPolicy` | 활성 조건 |
|---|---|---|
| 파티원 | `AlwaysShow` | 게임 진행 중 항상 (in-screen: 이름표 / off-screen: 가장자리 인디케이터) |
| 보스 | `OffScreenOnly` | 보스 살아있고 보스전 진행 중 + 화면 밖일 때만. 화면 안에 있으면 인디케이터 숨김 (보스 본체 보임) |
| 퀘스트 (R11 외) | `WhileActive` | 퀘스트 활성 중. **R11 범위 외 — 인터페이스만, 실 등록은 퀘스트 시스템 구현 시점** |

## 4. 표시 규칙 — 히스테리시스 (β 표준)

경계 깜빡임 방지를 위한 두 임계값 히스테리시스.

```
viewport = Camera.main.WorldToViewportPoint(targetWorldPos)   // [0,1] 가 화면

ε = 0.05  (튜닝 상수, GameplayConfig 노출 권장)

화면 영역  = [0, 1]              ← in-screen 진입 임계
큰 영역    = [-ε, 1 + ε]         ← off-screen 이탈 임계
```

**전환 규칙:**

| 현재 모드 | 다음 모드 | 조건 |
|---|---|---|
| in-screen | off-screen | viewport ∉ [-ε, 1+ε] (큰 영역 밖) |
| off-screen | in-screen | viewport ∈ [0, 1] (화면 안) |
| 그 외 | **유지** | 두 임계값 사이는 이전 상태 유지 — 깜빡임 0 |

**Z 가드:** `viewport.z < 0` (카메라 뒤) 는 무조건 off-screen. 2D 카메라 환경에선 비정상 케이스지만 안전 가드.

**최초 진입 처리:** `Mode.OffScreen` 으로 초기화 → 첫 프레임 평가에서 자연스럽게 결정.

## 5. UI 모드

### 5-1. In-Screen Mode (월드 스페이스)

화면 안에 있을 때:
- **머리 위 이름 텍스트만.** 마커/아이콘 없음 (사용자 결정).
- 월드 스페이스 Canvas, 캐릭터 위치 + `(0, 0.5, 0)` 오프셋.
- 텍스트 색 = 플레이어 indicatorColor (§ 6 참조).
- 가독성: 검정 외곽선 (TMP `Outline`) 또는 그림자.

대기실 [waiting-room.md § 3](waiting-room.md) 의 LobbyPlayer 오버헤드 UI 와 같은 구조 (이름표만 떼어낸 형태).

### 5-2. Off-Screen Mode (스크린 스페이스)

화면 밖에 있을 때:
- **방향 화살표** (작은 삼각형) + **테두리 색** = 플레이어 indicatorColor
- **아래 이름 텍스트** (작게)
- 화면 가장자리에 클램프 — 카메라 중심에서 타겟 방향 벡터를 viewport 사각형 경계에 교차

**가장자리 클램프 수식:**

```csharp
Vector3 worldPos = target.Transform.position;
Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

// 카메라 뒤일 경우 방향 반전
if (screenPos.z < 0)
{
    screenPos.x = Screen.width  - screenPos.x;
    screenPos.y = Screen.height - screenPos.y;
}

Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
Vector2 dir = ((Vector2)screenPos - screenCenter);
if (dir.sqrMagnitude < 0.001f) dir = Vector2.up;
dir.Normalize();

// 안쪽 마진 (인디케이터 아이콘이 화면에서 살짝 안쪽으로)
const float Padding = 40f;
float halfW = Screen.width  * 0.5f - Padding;
float halfH = Screen.height * 0.5f - Padding;

// 사각형 경계와 dir 의 교차
float t = Mathf.Min(
    halfW / Mathf.Abs(dir.x == 0 ? 1e-4f : dir.x),
    halfH / Mathf.Abs(dir.y == 0 ? 1e-4f : dir.y));
Vector2 clamped = screenCenter + dir * t;

indicatorRect.position = clamped;

// 화살표 회전 — 위쪽이 진행방향
indicatorRect.localEulerAngles = new Vector3(0, 0,
    Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
```

## 6. 색상 출처 — `PlayerColorPalette`

ActorNumber 기준 슬롯 팔레트. 같은 캐릭터 2명이 골라도 색 충돌 없음.

```csharp
// Features/UI/Adapter/Indicator/PlayerColorPalette.cs
public static class PlayerColorPalette
{
    private static readonly Color[] Palette =
    {
        new Color(0.30f, 0.69f, 1.00f),  // 파랑
        new Color(1.00f, 0.45f, 0.45f),  // 빨강
        new Color(0.45f, 0.85f, 0.45f),  // 초록
        new Color(1.00f, 0.85f, 0.30f),  // 노랑
    };

    public static Color Get(int actorNumber)
        => Palette[((actorNumber - 1) % Palette.Length + Palette.Length) % Palette.Length];
}
```

ActorNumber 는 Photon 에서 1부터 시작. 음수 안전 처리 포함. 보스는 별도 — `Color.red` 고정.

**대안 검토 (보류):** `CharacterData.indicatorColor` 필드로 캐릭터별 고정 색. 슬롯 색이 파티 구분에 더 직관적이라 1차는 슬롯 색 채택.

## 7. 4인 모서리 겹침 처리

같은 모서리에 여러 명 인디케이터가 모일 때:
- **테두리 색 + 이름 텍스트** 둘 다 노출 (구분 정보 중복 — 안전)
- 1차는 **단순 겹침 허용.** 시각 잡음이 실측에서 문제되면 후속:
  - 인접 인디케이터 감지 시 ±N px 오프셋 분산 (별건 작업)

## 8. 폴더 구조

```
Assets/Scripts/Features/UI/
├── Adapter/Indicator/
│   ├── IWorldIndicatorTarget.cs      ← 추적 대상 인터페이스
│   ├── IndicatorPolicy.cs            ← enum (AlwaysShow / OffScreenOnly / WhileActive)
│   ├── PlayerColorPalette.cs         ← 정적 슬롯 색 팔레트
│   └── WorldIndicatorManager.cs      ← 싱글턴, 등록/해제 + View 풀
└── Presentation/Indicator/
    └── WorldIndicatorView.cs         ← 마커 1개 시각 + 히스테리시스 상태머신
```

**프리팹 (신규):**
- `Assets/Resources/Prefabs/UI/WorldIndicator.prefab` — 자식 2개 (`OnScreenRoot`, `OffScreenRoot`) 토글
  - `OnScreenRoot` (월드 스페이스): TMP_Text (이름)
  - `OffScreenRoot` (스크린 스페이스): Image (화살표) + Image (테두리, 색 적용 대상) + TMP_Text (이름)

**어댑터 (신규):**
- `Assets/Scripts/Features/Character/Adapter/PartyMemberIndicatorAdapter.cs`
- `Assets/Scripts/Features/Boss/Adapter/BossIndicatorAdapter.cs`

## 9. 인터페이스

```csharp
// Features/UI/Adapter/Indicator/IWorldIndicatorTarget.cs
using UnityEngine;

namespace SwDreams.Features.UI.Adapter.Indicator
{
    /// <summary>
    /// 월드 인디케이터로 추적할 대상이 구현. 파티원/보스/(추후)퀘스트 가 구현체.
    /// Transform 노출하므로 Domain 에 두지 않고 Adapter 분류.
    /// </summary>
    public interface IWorldIndicatorTarget
    {
        Transform Transform { get; }
        string DisplayName { get; }
        Color IndicatorColor { get; }
        IndicatorPolicy Policy { get; }

        /// <summary>false 시 인디케이터 숨김. 보스: 살아있고 보스전 동안만 / 퀘스트: 활성 시만.</summary>
        bool IsActive { get; }
    }
}
```

```csharp
// Features/UI/Adapter/Indicator/IndicatorPolicy.cs
namespace SwDreams.Features.UI.Adapter.Indicator
{
    public enum IndicatorPolicy
    {
        AlwaysShow    = 0,  // in-screen + off-screen 모두 표시
        OffScreenOnly = 1,  // 화면 밖일 때만 (in-screen 시 인디케이터 숨김)
        WhileActive   = 2,  // 퀘스트용. IsActive==true 동안만 + OffScreenOnly 와 결합
    }
}
```

## 10. WorldIndicatorManager

```csharp
public class WorldIndicatorManager : MonoBehaviour
{
    public static WorldIndicatorManager Instance { get; private set; }

    [SerializeField] private GameObject indicatorPrefab;
    [SerializeField] private Canvas worldCanvas;     // 월드 스페이스 (in-screen 이름표 부모)
    [SerializeField] private Canvas screenCanvas;    // 스크린 스페이스 Overlay (off-screen 화살표 부모)

    private readonly Dictionary<IWorldIndicatorTarget, WorldIndicatorView> views = new();

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Register(IWorldIndicatorTarget target)
    {
        if (target == null || views.ContainsKey(target)) return;
        var go = Instantiate(indicatorPrefab);
        var view = go.GetComponent<WorldIndicatorView>();
        view.Initialize(target, worldCanvas, screenCanvas);
        views[target] = view;
    }

    public void Unregister(IWorldIndicatorTarget target)
    {
        if (target == null || !views.TryGetValue(target, out var view)) return;
        if (view != null) Destroy(view.gameObject);
        views.Remove(target);
    }
}
```

GameScene 진입점에 1개 배치. **DontDestroyOnLoad 안 함** — 씬마다 재초기화 (메뉴씬에서는 불필요).

## 11. 등록 / 해제 후크

| 대상 | 등록 위치 | 해제 위치 |
|---|---|---|
| 파티원 (자기 외) | `PartyMemberIndicatorAdapter.Start()` — `photonView.IsMine == false` 일 때만 | `OnDestroy` |
| 보스 | `BossIndicatorAdapter.Awake()` | `OnDestroy` (보스 사망 시 GameObject 파괴) |
| 퀘스트 (R11 외) | 퀘스트 활성 시 | 퀘스트 종료 시 |

**자기 자신 등록 안 함:** 자기는 항상 화면 중앙. `IsMine` 가드.

### 11-1. PartyMemberIndicatorAdapter

```csharp
// Features/Character/Adapter/PartyMemberIndicatorAdapter.cs
using Photon.Pun;
using UnityEngine;
using SwDreams.Features.UI.Adapter.Indicator;

namespace SwDreams.Features.Character.Adapter
{
    [RequireComponent(typeof(PhotonView))]
    public class PartyMemberIndicatorAdapter : MonoBehaviour, IWorldIndicatorTarget
    {
        private PhotonView pv;

        public Transform Transform => transform;
        public string DisplayName  => pv?.Owner?.NickName ?? "Player";
        public Color IndicatorColor => pv?.Owner != null
            ? PlayerColorPalette.Get(pv.Owner.ActorNumber)
            : Color.white;
        public IndicatorPolicy Policy => IndicatorPolicy.AlwaysShow;
        public bool IsActive => true;

        private void Awake() => pv = GetComponent<PhotonView>();

        private void Start()
        {
            if (pv != null && !pv.IsMine)
                WorldIndicatorManager.Instance?.Register(this);
        }

        private void OnDestroy() => WorldIndicatorManager.Instance?.Unregister(this);
    }
}
```

→ Player 프리팹에 컴포넌트 추가. `GamePlayerSpawner` 가 Player 프리팹 인스턴스화 시 자동 부착.

### 11-2. BossIndicatorAdapter

```csharp
// Features/Boss/Adapter/BossIndicatorAdapter.cs
using UnityEngine;
using SwDreams.Features.UI.Adapter.Indicator;

namespace SwDreams.Features.Boss.Adapter
{
    [RequireComponent(typeof(Boss))]
    public class BossIndicatorAdapter : MonoBehaviour, IWorldIndicatorTarget
    {
        private Boss boss;

        public Transform Transform => transform;
        public string DisplayName  => "Boss";   // Phase D 로컬라이제이션 시 키로 교체
        public Color IndicatorColor => Color.red;
        public IndicatorPolicy Policy => IndicatorPolicy.OffScreenOnly;

        public bool IsActive => boss != null && boss.IsAlive
            && SwDreams.Shared.Managers.GameManager.Instance != null
            && SwDreams.Shared.Managers.GameManager.Instance.CurrentState
               == SwDreams.Shared.Managers.GameManager.GameState.BossFight;

        private void Awake()
        {
            boss = GetComponent<Boss>();
            WorldIndicatorManager.Instance?.Register(this);
        }

        private void OnDestroy() => WorldIndicatorManager.Instance?.Unregister(this);
    }
}
```

→ Boss 프리팹에 컴포넌트 추가.

## 12. WorldIndicatorView (히스테리시스 상태머신)

```csharp
// Features/UI/Presentation/Indicator/WorldIndicatorView.cs
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SwDreams.Features.UI.Adapter.Indicator;

namespace SwDreams.Features.UI.Presentation.Indicator
{
    public class WorldIndicatorView : MonoBehaviour
    {
        private enum Mode { InScreen, OffScreen }

        [Header("On-Screen (월드 스페이스)")]
        [SerializeField] private GameObject onScreenRoot;
        [SerializeField] private TMP_Text   onScreenName;

        [Header("Off-Screen (스크린 스페이스)")]
        [SerializeField] private GameObject offScreenRoot;
        [SerializeField] private TMP_Text   offScreenName;
        [SerializeField] private Image      arrowImage;
        [SerializeField] private Image      borderImage;
        [SerializeField] private float      edgePadding = 40f;

        private const float Margin = 0.05f;
        private const float NameOffsetY = 0.5f;   // in-screen 머리 위 오프셋

        private IWorldIndicatorTarget target;
        private Mode currentMode = Mode.OffScreen;
        private RectTransform onScreenRect;
        private RectTransform offScreenRect;

        public void Initialize(IWorldIndicatorTarget t, Canvas worldCanvas, Canvas screenCanvas)
        {
            target = t;
            onScreenRoot.transform.SetParent(worldCanvas.transform, false);
            offScreenRoot.transform.SetParent(screenCanvas.transform, false);
            onScreenRect = onScreenRoot.GetComponent<RectTransform>();
            offScreenRect = offScreenRoot.GetComponent<RectTransform>();

            onScreenName.text  = t.DisplayName;
            offScreenName.text = t.DisplayName;
            onScreenName.color = t.IndicatorColor;
            offScreenName.color = Color.white;
            borderImage.color  = t.IndicatorColor;
            arrowImage.color   = t.IndicatorColor;
        }

        private void LateUpdate()
        {
            if (target == null || target.Transform == null || !target.IsActive)
            {
                onScreenRoot.SetActive(false);
                offScreenRoot.SetActive(false);
                return;
            }

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 vp = cam.WorldToViewportPoint(target.Transform.position);

            bool insideOuter  = vp.z > 0
                && vp.x > -Margin && vp.x < 1 + Margin
                && vp.y > -Margin && vp.y < 1 + Margin;
            bool insideScreen = vp.z > 0
                && vp.x >= 0 && vp.x <= 1
                && vp.y >= 0 && vp.y <= 1;

            // 히스테리시스 전환
            if (currentMode == Mode.InScreen && !insideOuter) currentMode = Mode.OffScreen;
            else if (currentMode == Mode.OffScreen && insideScreen) currentMode = Mode.InScreen;

            // 정책 적용
            bool showInScreen  = currentMode == Mode.InScreen
                              && target.Policy != IndicatorPolicy.OffScreenOnly;
            bool showOffScreen = currentMode == Mode.OffScreen;

            onScreenRoot.SetActive(showInScreen);
            offScreenRoot.SetActive(showOffScreen);

            if (showInScreen)  UpdateOnScreen();
            if (showOffScreen) UpdateOffScreen(cam);
        }

        private void UpdateOnScreen()
        {
            onScreenRect.position = target.Transform.position + Vector3.up * NameOffsetY;
        }

        private void UpdateOffScreen(Camera cam)
        {
            Vector3 sp = cam.WorldToScreenPoint(target.Transform.position);
            if (sp.z < 0) { sp.x = Screen.width - sp.x; sp.y = Screen.height - sp.y; }

            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 dir = ((Vector2)sp - center);
            if (dir.sqrMagnitude < 0.001f) dir = Vector2.up;
            dir.Normalize();

            float halfW = Screen.width  * 0.5f - edgePadding;
            float halfH = Screen.height * 0.5f - edgePadding;

            float dx = Mathf.Abs(dir.x) < 1e-4f ? 1e-4f : Mathf.Abs(dir.x);
            float dy = Mathf.Abs(dir.y) < 1e-4f ? 1e-4f : Mathf.Abs(dir.y);
            float t  = Mathf.Min(halfW / dx, halfH / dy);

            offScreenRect.position = center + dir * t;
            offScreenRect.localEulerAngles = new Vector3(0, 0,
                Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
        }
    }
}
```

## 13. 네트워크

[network-sync.md](network-sync.md) 참조.

- **클라이언트 로컬.** Photon 동기화 안 함.
- 추적 대상 위치는 이미 `PhotonTransformView` / Boss RPC 로 동기화됨 — 인디케이터는 그 결과를 읽어 렌더만.
- 각 클라가 자기 카메라 기준으로 자기 인디케이터 계산.
- RPC 추가 없음 → `photon-sync-auditor` 호출 불필요. 단 후크 위치 검증 차원에서 호출 가능.

## 14. 도메인 순수성 / 아키텍처 규칙

| 파일 | 허용 import | 비고 |
|---|---|---|
| `Adapter/Indicator/IWorldIndicatorTarget.cs` | `UnityEngine` | Transform 노출이 Adapter 위치 정당화 |
| `Adapter/Indicator/IndicatorPolicy.cs` | (없음) | 순수 enum |
| `Adapter/Indicator/PlayerColorPalette.cs` | `UnityEngine` | Color 사용 |
| `Adapter/Indicator/WorldIndicatorManager.cs` | `UnityEngine` | MonoBehaviour |
| `Presentation/Indicator/WorldIndicatorView.cs` | `UnityEngine`, `TMPro`, `UnityEngine.UI` | UI 컴포넌트 |
| `Character/Adapter/PartyMemberIndicatorAdapter.cs` | `UnityEngine`, `Photon.Pun` | Adapter 레이어 OK |
| `Boss/Adapter/BossIndicatorAdapter.cs` | `UnityEngine` | - |

본 시스템은 Domain 레이어를 두지 않음 (UI 보조 시스템). `architecture-guardian` 통과 가능.

## 15. 검증 시나리오 (구현 시 사용)

ParrelSync 4 인스턴스 기준. **체크리스트화 된 작업 항목은 [implementation-roadmap.md § R11 Phase 3](../architecture/implementation-roadmap.md) 참조** — 본 섹션은 시나리오 정의.

**파티원 인디케이터:**
- 자기 인디케이터 안 뜸 (`IsMine` 가드)
- 다른 3명이 화면 밖일 때 가장자리 표시, 안일 때 머리 위 이름
- 4명 같은 모서리에 모이면 색상 4종으로 구분됨
- 한 명 disconnect 시 인디케이터 즉시 사라짐

**보스:**
- 보스 등장 시 등록, 사망 시 해제
- 화면 안에 있을 때 인디케이터 안 보임 (OffScreenOnly)
- 페이즈 이동 패턴으로 화면 밖 이탈 시 가장자리 표시
- 비상 보스전 (호스트 마이그레이션) 후에도 정상 동작

**히스테리시스:**
- 화면 경계에서 좌우로 ε 픽셀 진동시켜도 모드 깜빡임 없음
- 카메라 뒤 (z<0) 안전 가드 동작

**아키텍처:**
- `architecture-guardian` 통과
- (선택) `photon-sync-auditor` — RPC 변경 없지만 후크 위치 검증

## 16. 알려진 제약

- [ ] `edgePadding` 픽셀 고정 — 화면 비율 변경(리사이즈) 시 비율 보존 안 됨. 추후 short-side 기준으로 변경 검토
- [ ] 4인 모서리 밀집 극단 케이스 — 색만으로 구분 어려울 수 있음. 인접 시 오프셋 분산 (별건 작업)
- [ ] 카메라 회전 시 viewport 좌표 비표준화 — Sweepin' Dreams 카메라는 회전 없음 가정
- [ ] `Camera.main` 참조 — 메인 카메라가 `MainCamera` 태그를 유지해야 함

## 17. 기존 코드 참조

| 파일 | 용도 |
|---|---|
| `Assets/Scripts/Features/Character/Adapter/GamePlayerSpawner.cs` | Player 프리팹 인스턴스화 — `PartyMemberIndicatorAdapter` 부착 위치 확인 |
| `Assets/Scripts/Features/Boss/Adapter/Boss.cs` / `BossSpawner.cs` | Boss 프리팹 인스턴스화 — `BossIndicatorAdapter` 부착 위치 |
| `Assets/Scripts/Features/Boss/Presentation/BossHealthBarUI.cs` | UI 분리 패턴 레퍼런스 |
| `Assets/Scripts/Features/Pickup/Presentation/InteractionPromptUI.cs` | 월드 스페이스 UI 패턴 레퍼런스 |
| `Assets/Scripts/Shared/Managers/NetworkManager.cs` | ActorNumber / NickName 출처 |
| `docs/systems/waiting-room.md` | 대기실 LobbyPlayer 오버헤드 UI 패턴 (재사용 검토) |

## 18. 비범위 (R11 에서 안 함)

- 퀘스트 인디케이터 실 등록 (`WhileActive` 정책 자체는 구현, 어댑터는 퀘스트 시스템 구현 시점)
- 인접 인디케이터 오프셋 분산 알고리즘
- 화면 short-side 기준 padding 정규화
- Localization 적용 (Boss `DisplayName` 의 "Boss" 문자열은 Phase 8-5 Localization C 시점에 키로 교체)
- 미니맵 (별도 시스템)

## 19. 변경 이력

- **2026-04-26:** 초안 작성. 사용자 결정 — In-Screen 은 이름표만(마커 없음), Off-Screen 은 화살표+테두리색+아래이름. 히스테리시스 β 표준. 파티원=AlwaysShow / 보스=OffScreenOnly. 색은 ActorNumber 슬롯 팔레트 4색. 퀘스트는 인터페이스만 깔고 실 등록 보류.
- **2026-04-26 (구현 완료):** 랜덤 퀘스트 인디케이터까지 R11 범위에 흡수 — `QuestData.isRandom` 플래그 + `QuestIndicatorAdapter` (OffScreenOnly, 자주색). `WorldIndicatorManager` 가 정적 `pendingTargets` 큐 + `RegisterTarget`/`UnregisterTarget` 정적 메서드로 Awake race 구조적 차단(어댑터가 Manager Awake 전에 호출해도 큐에 적재 → Manager Awake 시 drain). 인디케이터 어댑터 3종은 폴링 없이 단순 호출 패턴.
