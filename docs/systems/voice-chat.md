# Voice Chat — Photon Voice 2 통합 가이드

Sweepin' Dreams 의 Co-op 보이스챗 시스템. 본 문서는 **다음 세션이 이 문서만 보고 구현을 끝낼 수 있도록** 작성되었다.

## 1. 메타

| 항목 | 값 |
|---|---|
| 시스템 ID | `voice-chat` |
| 분류 | 네트워크 / UX |
| 의존 레이어 | Adapter (Voice), Presentation (UI) |
| 의존 외부 패키지 | Photon Voice 2 (Asset Store 무료) |
| 최종 업데이트 | 2026-04-19 |
| 구현 상태 | ⬜ 미구현 (Phase A 문서화 단계) |

## 2. 결정 근거

**채택:** **Photon Voice 2** — 무료 티어 20 CCU.

| 비교 후보 | 무료 여부 | 멀티플랫폼 | 채택 안 한 이유 |
|---|---|---|---|
| **Photon Voice 2** | 20 CCU 무료 | ✅ 전 플랫폼 | (채택) PUN 2 인프라 재사용, AppId 공유 가능, PhotonView/PhotonVoiceView 자동 페어링 |
| Steam Voice API | 무료 | ❌ Steam 전용 | Stove 빌드에선 작동 X. 양쪽 따로 구현하면 유지보수 비용이 라이선스비보다 큼 |
| Stove Indie 보이스 | (미공개) | ❌ Stove 전용 | 공식 SDK 보이스 API 미공개. 파트너 문의 필요 |
| Vivox (Unity) | 무료 티어 있음 | ✅ | PUN Room과 별도 채널 관리 비용. 채널 ID 동기화 별도 구현 |
| Dissonance | 유료 ($95) | ✅ | 라이선스비 + Photon 통합이 Voice 2 만큼 매끄럽지 않음 |
| `UnityEngine.Microphone` 직접 | 무료 | - | 인코딩/지터버퍼/에코캔슬 자작 부담. 비현실적 |

**WHY 이 선택이 나중에 후회 없을 가능성:** PUN과 같은 회사·같은 SDK 기반이라 호스트 마이그레이션·재연결 시점에 추가 처리가 거의 없다. 출시 시 동접이 폭발해 유료로 가더라도 PUN 요금과 함께 산정되어 회계 처리도 단순.

## 3. 라이선스 / 비용

| 동시 접속 (CCU) | 요금 |
|---|---|
| ~20 CCU | **무료** (PUN AppId와 별도 Voice AppId 무료 발급) |
| 100 CCU | $9 / 월 (5 GB 트래픽 포함) |
| 500 CCU | $45 / 월 |

**중요:** PUN AppId 와 **Voice AppId는 별도** 발급. Photon 대시보드에서 "PhotonVoice" 종류로 새 앱 생성 후 AppId 복사. 같은 Region 사용 권장.

## 4. 인터페이스 / 컴포넌트 구조

Photon Voice 2 는 직접 코드 작성보다 **컴포넌트 부착 중심**이다. 작성할 코드는 권한·UI 후크 정도.

### 4-1. 씬 레벨 (1회 셋업)

`GameScene` 의 NetworkManager 와 같은 GameObject 또는 형제 GameObject 에:

| 컴포넌트 | 역할 |
|---|---|
| `PhotonVoiceNetwork` | Voice 서버 연결 관리. PUN 의 `PhotonNetwork` 와 동등한 역할 |
| (Inspector) `Settings.AppIdVoice` | 위 §3에서 발급받은 Voice AppId 입력 |
| (Inspector) `Settings.FixedRegion` | PUN 과 동일 Region (예: `kr`, `asia`) |
| (Inspector) `Settings.AutoConnect` | true (PUN 연결 시 자동 Voice 연결) |

### 4-2. Player 프리팹 변경

기존 Player 프리팹(이미 `PhotonView` 부착)에 **추가**:

| 컴포넌트 | 설정 |
|---|---|
| `PhotonVoiceView` | 기존 `PhotonView` 와 동일 GameObject. **ViewID 자동 공유** |
| `Recorder` | `TransmitEnabled = false` (시작 시 음소거). `MicrophoneType = Photon` 권장 (Unity Mic 보다 안정) |
| `Speaker` | `playOnAwake = false`. AudioSource 함께 부착 (자동 생성 가능) |

**왜 `TransmitEnabled = false` 시작:** 입장 직후 의도치 않은 마이크 송출 방지. UI에서 명시적 토글로 켠다.

### 4-3. 새로 작성할 C# (최소)

`Assets/Scripts/Features/Voice/` 폴더 신규.

```
Features/Voice/
├── Adapter/
│   ├── VoiceController.cs       ← Recorder 제어 (Mute/Unmute, PTT 입력 처리)
│   └── VoiceBootstrap.cs         ← PhotonVoiceNetwork 초기화 보조 (필요 시)
└── Presentation/
    └── VoiceIndicatorUI.cs       ← 누가 말하는 중인지 표시 (Speaker.IsPlaying 구독)
```

**`VoiceController.cs` 스켈레톤:**

```csharp
using UnityEngine;
using Photon.Voice.Unity;

namespace SwDreams.Features.Voice.Adapter
{
    /// <summary>
    /// Player 프리팹에 부착. Recorder 의 송신을 토글한다.
    /// 입력: Push-to-Talk 키 또는 토글 버튼 UI에서 호출.
    /// </summary>
    [RequireComponent(typeof(Recorder))]
    public class VoiceController : MonoBehaviour
    {
        [SerializeField] private KeyCode pushToTalkKey = KeyCode.V;
        [SerializeField] private bool pushToTalkMode = true;  // false = Open Mic 토글

        private Recorder recorder;
        private bool openMicEnabled = false;

        private void Awake() => recorder = GetComponent<Recorder>();

        private void Update()
        {
            if (!photonView_IsMine()) return;  // 자기 Recorder만 제어

            if (pushToTalkMode)
            {
                bool down = Input.GetKey(pushToTalkKey);
                if (recorder.TransmitEnabled != down)
                    recorder.TransmitEnabled = down;
            }
            else
            {
                if (recorder.TransmitEnabled != openMicEnabled)
                    recorder.TransmitEnabled = openMicEnabled;
            }
        }

        public void ToggleOpenMic() => openMicEnabled = !openMicEnabled;
        public void SetMode(bool ptt) => pushToTalkMode = ptt;

        private bool photonView_IsMine()
        {
            var pv = GetComponent<Photon.Pun.PhotonView>();
            return pv != null && pv.IsMine;
        }
    }
}
```

## 5. 설정 / 권한

### 5-1. 마이크 권한 (Android / iOS)

- `ProjectSettings/Player/Other Settings/Microphone Usage Description` 작성 (iOS 필수)
- Android: `<uses-permission android:name="android.permission.RECORD_AUDIO"/>` 자동 추가됨 (Photon Voice 가 처리)
- 런타임 권한 요청은 `Application.RequestUserAuthorization(UserAuthorization.Microphone)` (모바일)

### 5-2. PC (Stove / Steam)

별도 권한 없음. 단, Recorder 의 `MicrophoneDevice` 가 비어있으면 OS 기본 마이크 사용.

### 5-3. 입력 모드

- **PTT (Push-to-Talk):** 기본값 권장. 키: `V`. 사용자 설정에서 변경 가능
- **Open Mic:** 토글. VAD(Voice Activity Detection)는 Recorder 의 `VoiceDetection = true` 로 활성화

## 6. UI 통합 지점

| 위치 | 추가할 것 | 파일 |
|---|---|---|
| 인게임 HUD | 자기 마이크 ON/OFF 토글 아이콘 | `Assets/Scripts/Features/UI/Adapter/InGameHUD.cs` (또는 후속) |
| 팀원 표시 | 누가 말하는 중 표시 (음성 파형 또는 아이콘) | `VoiceIndicatorUI.cs` 신규 |
| 옵션 메뉴 | PTT 키 변경, PTT/Open Mic 모드, 마이크 디바이스 선택, 입력 볼륨 | 옵션 패널 (현재 미구현 → 후속) |
| 결과 화면 | 보이스 활성화 여부는 표시 안 함 | - |

**WHY 옵션 메뉴 분리:** PTT 키 변경은 게임 로직과 무관하므로 옵션 패널 후속 작업과 함께 구현.

## 7. 거리 기반 볼륨 (Positional Voice) — 옵션

Co-op 4인이 같은 화면에 있는 Survivors-like 라 **기본 비활성** 권장.

활성화 시:
- `Speaker` GameObject 의 `AudioSource.spatialBlend = 1` (3D)
- `MaxDistance` = 카메라 가시 범위 약간 초과 (예: 30 unit)
- 보스전 등 분리 상황에서만 효과적 → 기본 OFF

## 8. 호스트 마이그레이션 / 재연결

PUN 호스트 마이그레이션 시 Voice 는 **자동 재연결**. 추가 코드 불필요.

단, `Recorder.TransmitEnabled` 상태는 **재연결 시 false 로 리셋될 수 있음** → `VoiceController` 가 재연결 콜백에서 자기 토글 상태 복원하도록 구현.

```csharp
// VoiceController.cs 추가 시그니처
private void OnEnable()
{
    PhotonVoiceNetwork.Instance.Client.StateChanged += OnVoiceStateChanged;
}

private void OnVoiceStateChanged(...) { /* 재연결 시 송신 상태 복원 */ }
```

## 9. 테스트 체크리스트

ParrelSync 4 인스턴스 기준.

- [ ] PUN 룸 입장 직후 Voice 도 자동 연결 (`PhotonVoiceNetwork.Client.State == ConnectedToMasterServer` → `Joined`)
- [ ] 기본 상태에서 마이크 송출 안 됨 (`Recorder.TransmitEnabled == false`)
- [ ] PTT 키 누르면 다른 인스턴스에서 음성 들림
- [ ] PTT 키 떼면 즉시 송출 중단
- [ ] Open Mic 모드로 토글 시 지속 송출
- [ ] 음소거 토글 시 즉시 중단
- [ ] 호스트 마이그레이션 발생 시 음성 재개 (5초 이내)
- [ ] 4명 동시 송출 시 끊김 없음
- [ ] PUN 룸 퇴장 시 Voice 도 끊김

## 10. 도메인 순수성 / 아키텍처

Voice 는 **Adapter / Presentation 레이어 한정**. Domain 에 음성 관련 코드 없음.

- `Features/Voice/Adapter/` — Photon Voice SDK 직접 사용 OK (`using Photon.Voice.Unity;` 허용)
- `Features/Voice/Presentation/` — Unity UI 사용 OK
- **Domain 레이어 만들지 않음** — 음성은 순수 인프라이며 게임 룰에 영향 안 줌

CLAUDE.md §2 의존성 방향 준수: Voice 는 다른 Feature 의 Domain 을 호출하지 않음. 호출 방향은 **UI(Presentation) → VoiceController(Adapter) → Photon Voice SDK** 단방향.

## 11. 구현 순서 (다른 세션이 따라야 할 단계)

1. **Photon 대시보드 작업** (사람이 함):
   - https://dashboard.photonengine.com 접속
   - "Create a New App" → Type: **Photon Voice**
   - App ID 복사
2. **Asset Store 임포트** (Unity 에서):
   - "PUN 2 - FREE" 이미 설치된 상태 가정
   - "Photon Voice 2" 검색 → Import
   - PUN 과 충돌 없음 (별도 네임스페이스)
3. **씬 셋업:**
   - `GameScene` 의 NetworkManager GameObject 에 `PhotonVoiceNetwork` 추가
   - Inspector 에 Voice AppId, Region 입력
4. **Player 프리팹 수정:**
   - `Assets/Resources/Prefabs/Player.prefab` (또는 실제 경로) 열기
   - `PhotonVoiceView`, `Recorder`, `Speaker` 컴포넌트 추가
   - Recorder 의 `TransmitEnabled = false` 설정
5. **VoiceController.cs 작성:** §4-3 스켈레톤 그대로 사용
6. **UI 후크:** §6 표 따라. 최소한 InGameHUD 에 토글 1개
7. **테스트:** §9 체크리스트
8. **CLAUDE.md §3 폴더 지도** 갱신: `Features/Voice/` 추가
9. **`docs/architecture/implementation-roadmap.md`** 의 해당 Phase 항목 ✅ 처리

## 12. 기존 코드 참조

- `Assets/Scripts/Shared/Managers/NetworkManager.cs` — PUN 연결 관리. PhotonVoiceNetwork 를 형제 GameObject 로 두기 위한 위치 확인
- `Assets/Resources/Prefabs/` — Player 프리팹 위치 (실제 파일명 확인 후 작업)
- `Assets/Scripts/Features/Character/Adapter/` — Player 관련 컴포넌트들. VoiceController 가 PhotonView 와 통신할 위치

## 13. 알려진 제약 / 주의

- [ ] **에코 캔슬:** Photon Voice 의 기본 인코더는 Opus. 헤드폰 미사용 시 에코 발생 가능 — 옵션 메뉴에 안내 권장
- [ ] **Stove 인디 빌드 인증:** Photon Voice 가 사용하는 마이크 권한·네트워크 도메인이 Stove 심사를 통과하는지 출시 전 검증 필요
- [ ] **CCU 한도 모니터링:** 무료 티어 20 CCU 초과 시 음성만 끊기는 게 아니라 룸 자체 영향 가능 → Photon 대시보드 알람 설정
- [ ] **모바일 빌드:** 본 프로젝트는 PC 우선이지만 향후 모바일 검토 시 마이크 권한 처리 재확인

## 14. 외부 참고

- Photon Voice 2 공식 문서: https://doc.photonengine.com/voice/current/getting-started/voice-intro
- Asset Store: "Photon Voice 2"
- Photon Dashboard: https://dashboard.photonengine.com
