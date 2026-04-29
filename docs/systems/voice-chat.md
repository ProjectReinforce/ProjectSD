# Voice Chat — Photon Voice 2 통합 가이드

Sweepin' Dreams 의 Co-op 보이스챗 시스템. 본 문서는 **다음 세션이 이 문서만 보고 구현을 끝낼 수 있도록** 작성되었다.

## 1. 메타

| 항목 | 값 |
|---|---|
| 시스템 ID | `voice-chat` |
| 분류 | 네트워크 / UX |
| 의존 레이어 | Adapter (Voice), Presentation (UI) |
| 의존 외부 패키지 | Photon Voice 2 (Asset Store 무료) |
| 최종 업데이트 | 2026-04-27 |
| 구현 상태 | ✅ 1차 통합 완료 (Phase 8-2) — 송수신 / PTT / OpenMic / 마이크 자체 테스트 동작. R3 마이크 필터 드랍은 후행 |

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

`MenuScene` 의 NetworkManager GameObject 에 부착 (DontDestroyOnLoad 따라감):

| 컴포넌트 | 역할 / 설정 |
|---|---|
| `PunVoiceClient` | Voice 서버 연결 관리. PUN 의 `PhotonNetwork` 와 동등한 역할. **구 명칭 `PhotonVoiceNetwork` 에서 변경됨 (Photon Voice 2 최신 버전)** |
| (Inspector) `Use Pun App Settings` | ✓ 기본값. `PhotonAppSettings.asset` 의 **App Id Voice** 칸을 자동 사용 (PUN AppId 와 별도). 별도 입력 칸 사라짐 |
| (Inspector) `Use Pun Auth Values` | ✓ 기본값 |
| `PhotonAppSettings.asset` | `App Id Voice` 칸에 발급받은 Voice AppId. `Fixed Region` 은 PUN 과 동일 (예: `kr`, `jp`). PUN 룸 follow 자동 (`LeaderInRoom => PhotonNetwork.InRoom`) — `_voice_` suffix 룸명 |

### 4-2. Player 프리팹 변경

기존 Player 프리팹(이미 `PhotonView` 부착)에 **추가**:

| 컴포넌트 | 설정 |
|---|---|
| `PhotonVoiceView` | 기존 `PhotonView` 와 동일 GameObject. **ViewID 자동 공유** |
| `Recorder` | `TransmitEnabled = false` (시작 시 음소거). `MicrophoneType = Photon` 권장 (Unity Mic 보다 안정) |
| `Speaker` | `playOnAwake = false`. AudioSource 함께 부착 (자동 생성 가능) |

**왜 `TransmitEnabled = false` 시작:** 입장 직후 의도치 않은 마이크 송출 방지. UI에서 명시적 토글로 켠다.

### 4-3. 작성된 C# (Phase 8-2 1차)

`Assets/Scripts/Features/Voice/` (실제 파일):

```
Features/Voice/
├── Adapter/
│   ├── VoiceController.cs       ← Recorder 제어 (Mute/PTT/OpenMic). 자기 PhotonView (IsMine) 만 제어. SettingsManager.OnMicChanged 구독해서 모드/감도 자동 반영. 새 Input System (Keyboard.current[Key.V]) 사용. 정적 LocalInstance 로 UI 가 인스펙터 드래그 없이 호출
│   └── MicTestService.cs         ← UnityEngine.Microphone 으로 직접 캡처 → AudioSource.loop 재생. Photon voice 룸 의존성 0 — 룸 미가입/메뉴씬/ParrelSync 무관. 설정 패널 "내 마이크 테스트" 토글이 호출
└── Presentation/
    └── MicToggleButton.cs       ← InGameHUD 의 마이크 ON/OFF 버튼 브리지. VoiceController.LocalInstance.ToggleMute() + 음소거 상태에 따라 sprite/색 자동 토글
```

**보류 (필요 시 추후):**
- `VoiceIndicatorUI` — 누가 말하는 중인지 시각화 (Speaker.IsPlaying 구독). 현재 미작성
- 빌드 환경에서 송수신 검증 + R3 마이크 필터 드랍과 함께 후행

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
    PunVoiceClient.Instance.Client.StateChanged += OnVoiceStateChanged;
}

private void OnVoiceStateChanged(...) { /* 재연결 시 송신 상태 복원 */ }
```

## 9. 테스트 체크리스트

ParrelSync 4 인스턴스 기준.

- [ ] PUN 룸 입장 직후 Voice 도 자동 연결 (`PunVoiceClient.Client.State == ConnectedToMasterServer` → `Joined`)
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

## 11. 구현 순서 — Phase 8-2 1차 통합 ✅ (2026-04-27)

1. ✅ **Photon 대시보드** — Voice AppId 발급
2. ✅ **Asset Store** — Photon Voice 2 임포트 (`Assets/FromStore/Photon/PhotonVoice/`)
3. ✅ **씬 셋업** — NetworkManager GameObject 에 `PunVoiceClient` 컴포넌트 추가. `PhotonAppSettings.asset` 의 `App Id Voice` 입력
4. ✅ **PlayerStub 프리팹** — `PhotonVoiceView` + `Recorder` (`TransmitEnabled = false`) + `Speaker` + `VoiceController` 부착
5. ✅ **VoiceController.cs** — 새 Input System 기반, `Keyboard.current[pushToTalkKey].isPressed`. SettingsManager.OnMicChanged 구독해서 모드/감도 자동 반영
6. ✅ **MicToggleButton** — InGameHUD 마이크 토글 버튼 (정적 `LocalInstance` 경유)
7. ✅ **MicTestService** — 단일 인스턴스 마이크 자체 테스트 (UnityEngine.Microphone 직접 캡처)
8. ✅ **micSensitivity 안전 클램프** — SettingsManager 에 `Mathf.Clamp(v, 0.01f, 1f)` (§13 함정 회피)
9. ✅ **검증** — 단일 인스턴스 마이크 테스트 동작 확인. ParrelSync 송수신은 micSensitivity 올린 후 정상 동작 확인. 빌드 환경 송수신 검증은 R3 작업과 함께 후행

**남은 후행 작업:** R3 (마이크 필터 드랍 아이템) — 빌드로 진짜 송수신 검증 + Speaker AudioFilter 부착 + RPC 동기화. [implementation-roadmap.md § R3](../architecture/implementation-roadmap.md) 참조.

## 11-MicTest. 마이크 자체 테스트 (MicTestService)

ParrelSync/멀티 인스턴스 의존 없이 자기 마이크 입력을 자기 Speaker 로 즉시 echo. **Photon Voice 의 voice 룸과 완전 무관** — 룸 미가입 상태(메뉴씬)에서도 동작.

**왜 Photon `Recorder.DebugEchoMode` 안 씀:** DebugEchoMode 는 "outgoing stream routed back to client via server" — voice 룸 서버 경유라 룸 미가입 시 동작 X. 메뉴씬에서 마이크 점검 가능해야 하므로 Unity 직접 캡처 채택.

**구조:**
```
MicTestService (DontDestroyOnLoad 싱글턴, GetOrCreate 자동 생성)
├── Microphone.Start(deviceName, loop=true, 1s, 44100Hz)
├── AudioSource.loop = true, volume = 0.7 (feedback 완화)
└── StartTest() / StopTest() / Toggle() public API
```

**SettingsPanelUI 통합:** Audio 섹션의 `micTestToggle` (Toggle) 이 `OnMicTestToggled(bool)` 호출 → MicTestService.Toggle. 패널 Hide() 시 강제 종료. 패널 열 때 항상 OFF 시작 (PlayerPrefs 미저장).

**한계:**
- 동일 OS 마이크 디바이스를 Photon Recorder 와 동시 사용 시 경합 가능 → 인게임 (Recorder 활성) 에선 비권장. 메뉴씬 마이크 점검 용도로 설계
- AudioSource.Output 이 master 라우팅 (AudioMixerGroup 미할당) — voiceGain 슬라이더 미적용. 마이크 테스트는 입력 검증 목적이라 OK

## 12. 기존 코드 참조

- `Assets/Scripts/Shared/Managers/NetworkManager.cs` — PUN 연결 관리. PunVoiceClient 를 형제 GameObject 로 두기 위한 위치 확인
- `Assets/Resources/Prefabs/` — Player 프리팹 위치 (실제 파일명 확인 후 작업)
- `Assets/Scripts/Features/Character/Adapter/` — Player 관련 컴포넌트들. VoiceController 가 PhotonView 와 통신할 위치

## 13. 알려진 제약 / 주의

- [x] **micSensitivity = 0 함정** (Phase 8-2 통합 검증 시 발견) — `Recorder.VoiceDetectionThreshold` 가 0 이면 OpenMic 모드에서 voice publish 자체가 차단되거나 silent frame 만 송출됨. SettingsManager.SetMicSensitivity 에 `Mathf.Clamp(v, 0.01f, 1f)` floor 적용. 슬라이더 minValue 도 0.01 권장
- [x] **PhotonVoiceNetwork → PunVoiceClient 명칭 변경** — Photon Voice 2 최신 버전부터. AppIdVoice 입력 위치도 컴포넌트 인스펙터 → `PhotonAppSettings.asset` 으로 이전 (`Use Pun App Settings = true` 기본)
- [x] **새 Input System 호환** — VoiceController 의 PTT 키 처리는 `Keyboard.current[Key.V].isPressed` (legacy `Input.GetKey` 금지). 이 프로젝트 표준 패턴 따름
- [ ] **에코 캔슬:** Photon Voice 의 기본 인코더는 Opus. 헤드폰 미사용 시 에코 발생 가능 — 옵션 메뉴에 안내 권장
- [ ] **Stove 인디 빌드 인증:** Photon Voice 가 사용하는 마이크 권한·네트워크 도메인이 Stove 심사를 통과하는지 출시 전 검증 필요
- [ ] **CCU 한도 모니터링:** 무료 티어 20 CCU 초과 시 음성만 끊기는 게 아니라 룸 자체 영향 가능 → Photon 대시보드 알람 설정
- [ ] **모바일 빌드:** 본 프로젝트는 PC 우선이지만 향후 모바일 검토 시 마이크 권한 처리 재확인
- [ ] **ParrelSync 환경에서 송수신 검증의 한계:** 같은 OS 마이크/같은 PhotonAppSettings 공유로 인해 미묘한 매핑 이슈가 발생할 수 있음. 진짜 송수신 검증은 빌드 + 다른 PC 또는 빌드 1 + 에디터 1 권장. 자체 마이크 입력 검증은 § 14-MicTestService 로 단일 인스턴스에서 가능
- [x] **VoiceFollowClient self-recursive race — 룸 떠날 때마다 LogError (cosmetic)** (2026-04-29 발견)
  - **증상:** `Operation LeaveRoom (254) not allowed on current server (MasterServer)` LogError. PUN 룸 떠날 때마다 100% 재현.
  - **원인:** `VoiceFollowClient.OnVoiceStateChanged` 가 자기 state 전이 도중(룸 → MasterServer 전환) 자체-recursive 로 `FollowLeader` 호출 → 자기는 이미 MasterServer 인데 OpLeaveRoom 시도 → `CheckIfOpCanBeSent` 가 거부. **Photon Voice 2 라이브러리 내부 callback chain 의 race**.
  - **영향:** 음성 송수신 동작 영향 0. LogError 만 발생.
  - **워크어라운드 시도 (효과 없음):** `PhotonNetwork.LeaveRoom` 직전에 `PunVoiceClient.Instance.Client.OpLeaveRoom(false)` 명시 호출 → Voice 가 자기 state 전이 안에서 자체적으로 follow 트리거하므로 외부 사전 leave 가 무효. 라이브러리 내부 race 라 외부 코드로 해결 불가능.
  - **처리:** **무시.** 인디 게임 스코프상 Photon 라이브러리 cosmetic 이슈에 시간 투입 비효율. Photon Voice 업데이트 시 자동 해소 가능성. 정 거슬리면 `Application.logMessageReceivedThreaded` 콜백으로 특정 메시지 필터링 가능하나 다른 Voice 에러 놓칠 위험으로 비추천.

## 14. 외부 참고

- Photon Voice 2 공식 문서: https://doc.photonengine.com/voice/current/getting-started/voice-intro
- Asset Store: "Photon Voice 2"
- Photon Dashboard: https://dashboard.photonengine.com
