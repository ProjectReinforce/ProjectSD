# Scene Structure — MenuScene / GameScene

Sweepin' Dreams 의 씬 구조와 각 씬 내부 패널 구성.

## 1. 메타

| 항목 | 값 |
|---|---|
| 시스템 ID | `scene-structure` |
| 분류 | 아키텍처 / UI |
| 의존 레이어 | Adapter (UI, Manager) |
| 최종 업데이트 | 2026-04-18 |

## 2. 목적

씬 구성과 전환 정책을 한 곳에 정의. UI 패널 전환·씬 전환·DontDestroyOnLoad 오브젝트 목록을 SSOT 로 유지.

## 3. 씬 구성 (2개)

| 씬 | 포함 화면 | 설명 |
|---|---|---|
| **MenuScene** | 타이틀 / 방 리스트 / 대기실 | Photon 접속 상태를 유지한 채 UI 패널 전환 |
| **GameScene** | 인게임 / 결과 화면 | 실제 플레이 + 결과. 결과 화면은 오버레이 |

### 2개로 나눈 이유

- **메뉴 씬 통합** — 타이틀/방 리스트/대기실은 모두 Photon 방 접속 상태에서 작동. 씬 전환 없이 UI 패널만 전환하면 네트워크 상태 유지.
- **게임 씬 분리** — 인게임 오브젝트(적, 보스, 투사체 등)를 메뉴와 완전히 분리 → 메모리 관리·초기화가 깔끔.
- **결과 화면 통합** — 게임 씬 안에 두면 게임 오브젝트를 배경 활용 가능 (클리어 연출 등).

## 4. MenuScene 패널 구조

**한 번에 하나만 활성화.**

| 패널 | 포함 UI | 전환 조건 |
|---|---|---|
| **TitlePanel** | 로고, 혼자하기, 같이하기, 설정, 종료, 버전 | 씬 최초 로드 시. 방 퇴장 시 복귀 |
| **RoomListPanel** | 방 리스트, 방 만들기/코드 입장 팝업, 새로고침, 뒤로가기 | 같이하기 + Photon 접속 성공 |
| **WaitingRoomPanel** | 월드 공간 캐릭터(WASD 이동, 오버헤드 이름/호스트/준비), 참가자 리스트(호스트 Kick), 캐릭터 프리뷰/변경 팝업, 준비 토글, 호스트 수동 Start + 카운트다운, 나가기 | CreateRoom / JoinRoom 성공 |

### 전환 흐름

```
[TitlePanel]
├─ 혼자하기 → Photon 접속 + CreateRoom(max=1) → [WaitingRoomPanel]
└─ 같이하기 → Photon 접속 → [RoomListPanel]

[RoomListPanel]
├─ 방 만들기 / 방 참가 / 코드 입장 → [WaitingRoomPanel]
└─ 뒤로가기 → Photon 해제 → [TitlePanel]

[WaitingRoomPanel]
├─ 전원 준비 → 3초 카운트다운 → PhotonNetwork.LoadLevel("GameScene")
└─ 나가기 → LeaveRoom → [RoomListPanel] (혼자하기는 [TitlePanel])
```

패널 전환 구현은 `MenuSceneManager`. 상세는 [managers.md § 7](managers.md).

**대기실 내부 구조(월드 공간 캐릭터 / 오버헤드 UI / 호스트 Kick·Start·카운트다운)는** [waiting-room.md](waiting-room.md) 참조.

## 5. GameScene 패널 구조

| 패널 | 포함 UI | 전환 조건 |
|---|---|---|
| **GameHUDPanel** | 체력/경험치 바, 타이머, 스킬 슬롯, 팀원 상태, 보스 체력, 레벨업/혼돈 선택 카드, 부활 타이머 | 씬 로드 시 활성. 게임 중 항상 표시 |
| **ResultPanel** (오버레이) | 결과 타이틀, 게임 통계, 빌드 요약, 보스 혼돈, 다시 하기/나가기 | 보스 처치 또는 전멸 시 GameHUD 위에 오버레이 |

UI 요소 상세는 [../game-design/flow-design.md § 2.4](../game-design/flow-design.md).

UI 프레임 시스템(레벨업 팝업 등)은 [ui-frame.md](ui-frame.md).

## 6. 씬 전환

### MenuScene → GameScene

호스트가 `PhotonNetwork.LoadLevel("GameScene")` 호출.
`PhotonNetwork.AutomaticallySyncScene = true` 설정으로 모든 클라이언트 씬이 자동 전환.

### GameScene → MenuScene

두 경로:

1. **다시 하기** (방 유지)
   ```
   Host: Room.SetCustomProperties({ returnToWaitingRoom: true })
   Host: PhotonNetwork.LoadLevel("MenuScene")
   All clients: MenuScene 로드 시 returnToWaitingRoom 체크
                → 플래그 있으면 WaitingRoomPanel 바로 활성화
                → 준비 상태 초기화(isReady = false), 플래그 제거
   ```
2. **나가기** (방 퇴장)
   ```
   PhotonNetwork.LeaveRoom()
   OnLeftRoom() → SceneManager.LoadScene("MenuScene")
   → TitlePanel 활성화 (기본 동작)
   ```

**핵심:** "다시 하기" 시 방을 나가지 않고 방 안에서 씬만 전환 → 팀원 흩어지지 않음.

## 7. DontDestroyOnLoad 오브젝트

씬 전환 시 파괴되지 않는 오브젝트:

| 오브젝트 | 역할 | 관리 방식 |
|---|---|---|
| **PhotonNetwork** | 네트워크 접속·방 상태 | Photon 자동 (DontDestroyOnLoad) |
| **NetworkManager** | Photon 콜백, 방 관리 로직 | DontDestroyOnLoad 싱글톤 |
| **SceneController** | 씬 전환 관리 | DontDestroyOnLoad 싱글톤 |
| **AudioManager** | BGM 연속 재생 | DontDestroyOnLoad 싱글톤 |

**주의:** 씬 한정 매니저(GameManager, UIManager, SpawnManager 등)는 DontDestroyOnLoad 사용 금지. 씬 로드마다 새로 생성.

## 8. 관련 코드

- `Assets/Scripts/Adapter/UI/MenuSceneManager.cs` (및 Menu 하위 컨트롤러)
- `Assets/Scripts/Adapter/UI/UImanager.cs`
- `Assets/Scripts/Adapter/Network/NetworkManager.cs`
- (신규) SceneController — 계획

## 9. 알려진 제약

- [ ] 다시 하기 시 팀원이 부분 퇴장한 경우의 빈 슬롯 처리 정책
- [ ] 씬 로드 중 연결 끊김 발생 시의 fallback 처리
