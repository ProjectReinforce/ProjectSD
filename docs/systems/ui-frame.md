# UI Frame System — FrameToast & (예정) Frame_PopUp

팝업/토스트 공용 UI 프레임워크 **설계**. 본 문서의 일부는 미구현 상태이며, 현재 구현과 목표를 분리해서 서술한다.

## 1. 메타

| 항목 | 값 |
|---|---|
| 시스템 ID | `ui-frame` |
| 분류 | UI |
| 의존 레이어 | Features/UI/Adapter, Features/UI/Presentation |
| 최종 업데이트 | 2026-04-19 |
| 구현 상태 | 🟡 부분 구현 (FrameToast 프리팹만 있음, Frame_PopUp·Frame 정적 API 미작성) |

## 2. 목적

화면 전환 없이 표시되는 **팝업 / 토스트 / 알림** 을 프레임 단위로 통일. 각 UI 요소가 개별 애니메이션/레이아웃을 재구현하는 대신, `Frame_PopUp` / `FrameToast` 를 **래핑**하여 일관된 룩과 생명주기를 갖는다.

## 3. 구성 요소 (현재 상태)

### 3.1 FrameToast ✅

- **용도:** 짧은 안내 메시지용 비모달 팝업.
- **위치:** `Assets/Resources/Prefabs/UI/FrameToast.prefab` (실재 ✓)
- **이미지 에셋:** `Assets/Resources/UI/window/box_toast.png`
- **동작:** 위에서 떨어져 일정 시간 후 페이드아웃.

### 3.2 Frame_PopUp ⬜ (미작성)

- **용도(예정):** 모달성 팝업 (레벨업 선택, 결과 화면, 설정 등).
- **현 상태:** **프리팹 없음.** `Assets/Resources/Prefabs/UI/Frame_PopUp.prefab` 파일 미존재.
- **현재 모달 UI 처리:** `LevelUpPanel.prefab` 과 같은 **개별 프리팹**이 직접 표시되며, 각 컨트롤러(`LevelUpPanel.cs`, `ResultPanelUI.cs` 등)가 표시/숨김을 관리. 일시정지는 `GameState.Paused` 로 제어 (`Time.timeScale = 0` 직접 사용 금지 — 멀티플레이 호환).
- **목표:** 단일 모달 프레임 + 컨텐츠 슬롯 패턴으로 통일.

## 4. 인터페이스 — 현재 vs 목표

### 4.1 현재 구현 (UIManager 직접 제어)

`Features/UI/Presentation/UImanager.cs` (파일명 소문자 m 주의) 가 각 패널을 **직접 SetActive 로 제어**한다. `Frame` 정적 API 클래스는 **존재하지 않는다.**

```csharp
// 실제 사용 예 (UImanager + LevelUpPanel)
UIManager.Instance.ShowLevelUp(choices);
UIManager.Instance.HideLevelUp();
UIManager.Instance.ShowResult(gameResult);
UIManager.Instance.HideResult();
```

### 4.2 목표 인터페이스 (미구현 — 도입 시 사용 예시)

```csharp
public static class Frame
{
    public static void ShowToast(string text, float duration = 3f);
    public static IPopupHandle ShowPopup(PopupContent content, bool pauseGame = true);
}

public interface IPopupHandle
{
    void Close();
    event Action OnClosed;
}
```

→ 도입 시 기존 `UIManager.ShowLevelUp/ShowResult` 등은 내부적으로 `Frame.ShowPopup` 을 호출하도록 마이그레이션.

## 5. 사용 위치 — 현재 vs 예정

| UI | 현재 | 목표 Frame 유형 |
|---|---|---|
| LevelUpPanel / SkillCardUI | `LevelUpPanel.prefab` 직접 표시 | Frame_PopUp (모달 + GameState.Paused) |
| 혼돈 스킬 선택 | (LevelUp 패널 재사용) | Frame_PopUp |
| 보스 등장 경고 | (별도 표시) | FrameToast (3초) |
| 자동 선택 안내 | (미구현) | FrameToast |
| 플레이어 연결 끊김 알림 | `ReconnectUI` | FrameToast |
| 결과 화면 | `ResultPanelUI` | Frame_PopUp (오버레이) |
| 사망 오버레이 | `DeathOverlayUI` | Frame_PopUp |

기존 `LevelUpPanel.cs` / `DeathOverlayUI.cs` / `ResultPanelUI.cs` / `ReconnectUI.cs` 등이 단계적으로 본 프레임을 사용하도록 이관 예정.

## 6. 생명주기 규칙

- **모달 팝업은 동시에 1개만.** 중첩 시 마지막 것만 보이도록 큐잉.
- **일시정지는 `GameState.Paused` 사용.** `Time.timeScale = 0` 직접 사용 금지 (멀티플레이 환경에서 호스트 권위가 필요하므로 GameManager.SetPaused 같은 경로로).
- **토스트는 비모달**, 최대 동시 표시 N개 (기본 3).
- 씬 전환 시 모든 프레임은 자동 소멸.

## 7. 네트워크 관련

- **팝업이 게임을 멈춘다면 호스트 승인 필요.** 클라이언트만 보는 UI(예: 개인 레벨업 선택지)는 `GameState.Paused` 에서만 노출.
- 네트워크 기본 규약은 [network-sync.md](network-sync.md).

## 8. 테스트

- 모달 중첩 시 큐 동작 확인
- 씬 전환 시 프레임 자동 정리
- 토스트 동시 표시 한도

## 9. 기존 코드 참조

- `Assets/Resources/Prefabs/UI/FrameToast.prefab` ✓ (실재)
- `Assets/Resources/UI/window/box_toast.png`
- `Assets/Scripts/Features/UI/Presentation/UImanager.cs` — 현재 UI 매니저
- `Assets/Scripts/Features/UI/Presentation/LevelUpPanel.cs`, `ResultPanelUI.cs`, `DeathOverlayUI.cs`, `ReconnectUI.cs` — 개별 패널들
- `Assets/Resources/Prefabs/UI/LevelUpPanel.prefab`, `ReconnectOverlay.prefab`, `DeathOverlay.prefab` — 현재 팝업 프리팹들

## 10. 알려진 제약 / 남은 작업

- [ ] **Frame_PopUp.prefab 미작성** — 모달 팝업 통합 프레임. 도입 시 LevelUpPanel/ResultPanel/DeathOverlay 등 이관
- [ ] **`Frame` 정적 API 클래스 미작성** — `ShowToast` / `ShowPopup` 진입점
- [ ] **FrameToast 호출하는 컴포넌트 미작성** — 현재는 프리팹만 있고 인스턴스화 코드 없음
- [ ] 모달 중첩 정책 확정 필요 (큐 vs 최상위만)
- [ ] 한 번에 토스트 최대 개수 결정 (기본 3 제안)
