# UI Frame System — FrameToast & Frame_PopUp

팝업/토스트 공용 UI 프레임워크. 최근 커밋에서 도입됨 — `6d6112763 feat: Frame_PopUp`, `84dfb3b3f feat: FrameToast`.

## 1. 메타

| 항목 | 값 |
|---|---|
| 시스템 ID | `ui-frame` |
| 분류 | UI |
| 의존 레이어 | Adapter/UI, Presentation |
| 최종 업데이트 | 2026-04-18 |

## 2. 목적

화면 전환 없이 표시되는 **팝업 / 토스트 / 알림** 을 프레임 단위로 통일. 각 UI 요소가 개별 애니메이션/레이아웃을 재구현하는 대신, `Frame_PopUp` / `FrameToast` 를 **래핑**하여 일관된 룩과 생명주기를 갖는다.

## 3. 구성 요소

### 3.1 Frame_PopUp

- **용도:** 모달성 팝업 (레벨업 선택, 결과 화면, 설정 등).
- **위치:** `Assets/Resources/Prefabs/UI/Frame_PopUp.prefab` (유사 경로)
- **동작:** 게임 일시정지(`Time.timeScale = 0`) 가능. DOTween 기반 페이드·스케일 연출.

### 3.2 FrameToast

- **용도:** **짧은 안내 메시지용 팝업** (커밋 메시지 발췌). 모달 아님.
- **위치:** `Assets/Resources/Prefabs/UI/FrameToast.prefab`
- **이미지 에셋:** `Assets/Resources/UI/window/box_toast.png`
- **동작:** 위에서 떨어져 일정 시간 후 페이드아웃.

## 4. 인터페이스 (설계)

*(실제 코드 시그니처는 코드 도입 시 추가될 수 있다. 현재는 **프리팹 기반**이며 전용 컴포넌트/매니저가 아직 Assets/Scripts 하위에 없음.)*

```csharp
// 예상 사용
public static class Frame
{
    public static void ShowToast(string text, float duration = 3f);
    public static IPopupHandle ShowPopup(PopupContent content, bool pauseTime = true);
}

public interface IPopupHandle
{
    void Close();
    event Action OnClosed;
}
```

## 5. 사용 위치 (예정)

| UI | Frame 유형 | 비고 |
|---|---|---|
| LevelUpPanel / SkillCardUI | Frame_PopUp | 모달 + 일시정지 |
| 혼돈 스킬 선택 | Frame_PopUp | 모달 |
| 보스 등장 경고 | FrameToast (3초) | 비모달 |
| 자동 선택 안내 | FrameToast | |
| 플레이어 연결 끊김 알림 | FrameToast | |
| 결과 화면 | Frame_PopUp (오버레이) | |

기존 `LevelUpPanel.cs` / `BossWarningUI.cs` / `DeathOverlayUI.cs` / `ResultPanelUI.cs` / `ReconnectUI.cs` 등이 단계적으로 본 프레임을 사용하도록 이관 예정.

## 6. 생명주기 규칙

- **모달 팝업은 동시에 1개만.** 중첩 시 마지막 것만 보이도록 큐잉.
- **Time.timeScale = 0** 를 설정하는 Frame_PopUp 은 호스트가 일시정지 상태를 동기화한다. 멀티플레이 상태에서는 `GameState.Paused` 와 연동.
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

- `Assets/Resources/Prefabs/UI/Frame_PopUp.prefab`
- `Assets/Resources/Prefabs/UI/FrameToast.prefab`
- `Assets/Resources/UI/window/box_toast.png`
- 기존 UI 스크립트: `Assets/Scripts/Adapter/UI/*.cs`

## 10. 알려진 제약

- [ ] 전용 매니저 스크립트 미작성. 호출 규격 API 확정 필요.
- [ ] 모달 중첩 정책 확정 필요 (큐 vs 최상위만).
- [ ] 한 번에 토스트 최대 개수 결정 (기본 3 제안).
