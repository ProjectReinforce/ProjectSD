# Waiting Room (대기실) 시스템

> 최종 갱신: 2026-04-19 · MenuScene의 "방 입장 후 게임 시작 전" 단계 전체를 담당한다.

## 1. 책임

- 방에 들어온 플레이어들의 **캐릭터를 월드 공간에 표시**하고 WASD로 이동 가능하게 한다 (인게임 이동과 동일한 감각).
- 각 캐릭터 위에 **이름 / 호스트 여부 / 준비 상태**를 오버헤드 UI로 노출.
- 플레이어 리스트(텍스트 행)를 별도로 제공 — 호스트에게만 **Kick(강퇴) 버튼** 활성.
- **호스트 수동 Start 버튼**: 전원 준비되면 활성화되며, 클릭 시 3·2·1 카운트다운 후 게임씬 진입.
- 대기실 입장 시 이전에 선택한 적 없는 플레이어는 **디폴트 캐릭터(id=0)** 로 세팅.
- 대기실 진입/퇴장에 맞춰 **타이틀 배경 이미지를 토글**해 월드 공간을 가리지 않게 한다.

## 2. 파일 맵

### 스크립트 (Adapter)
```
Features/UI/Adapter/Menu/
├── WaitingRoomPanelController.cs  ← 흐름 제어 허브 (Ready/Start/Countdown/Entries/Spawner)
├── MenuSceneManager.cs            ← 패널 전환 + titleBackground 토글
├── CharacterSelectUI.cs           ← 캐릭터 선택 팝업 (기존, 재사용)
├── LobbyPlayerController.cs       ← LobbyPlayer 루트: WASD 이동 + characterId 외관 반영
├── LobbyPlayerOverhead.cs         ← 캐릭터 위 WorldSpace UI: 이름/Host/Ready
├── LobbyPlayerSpawner.cs          ← 방 입장/퇴장 시 LobbyPlayer 스폰/파괴
└── LobbyPlayerEntry.cs            ← 리스트 1행: Name/Role/Char/Ready + Kick 버튼
```

### 프리팹
```
Assets/Resources/
└── LobbyPlayer.prefab              ← PhotonNetwork.Instantiate 대상 (경로 이름 "LobbyPlayer")

Assets/Resources/Prefabs/UI/
├── Panel_Lobby.prefab              ← 대기실 루트 패널 (기존, 수정)
└── LobbyPlayerEntry.prefab         ← 플레이어 리스트 1행
```

### 공통
- `Shared/Managers/NetworkManager.cs` — `KickPlayer(Player)` / `SetLocalCharacter/Ready` / `CanMasterStartGameInCurrentRoom` 등 제공.

## 3. 네트워크 모델

| 역할 | 주체 | 매체 |
|---|---|---|
| 본인 캐릭터 위치 동기화 | 각 클라 로컬 | `PhotonTransformView` (Unreliable) |
| 캐릭터 선택(characterId) | 각 클라 로컬 | `Player.CustomProperties["characterId"]` |
| 준비 상태(isReady) | 각 클라 로컬 | `Player.CustomProperties["isReady"]` |
| 카운트다운 | MasterClient | `Room.CustomProperties["startCountdownActive"/"Time"]` |
| 강퇴 | MasterClient | `PhotonNetwork.CloseConnection(player)` |
| 게임씬 로드 | MasterClient | `PhotonNetwork.AutomaticallySyncScene` + `SceneTransitionManager.EnterGameSceneByMaster()` |

## 4. Unity 에디터 셋업 (신규 작업 체크리스트)

### 4.1 LobbyPlayer.prefab 생성

1. 프로젝트 창 `Assets/Resources/` 폴더에서 Create → 2D Object 또는 빈 GameObject → 이름 `LobbyPlayer`.
2. 루트에 다음 컴포넌트 추가:
   - `PhotonView`
   - `PhotonTransformView` — `Synchronize Position: Yes`, Rotation/Scale: No
   - `Rigidbody2D` — Gravity Scale 0, Freeze Rotation Z, Interpolation: Interpolate
   - `CircleCollider2D` — 반지름 0.25 정도 (충돌 없길 원하면 Is Trigger 체크)
   - `SpriteRenderer` — 임시 스프라이트
   - `Animator` — Controller 슬롯 비워두기 (런타임에 `CharacterData.animatorController` 가 채움). Apply Root Motion: OFF
   - `LobbyPlayerController.cs`
3. `PhotonView.ObservedComponents`에 `PhotonTransformView` 드래그.
4. `LobbyPlayerController`의 `characterDB` 필드에 `Assets/Data/CharacterDatabase.asset` 연결.
   - `Default Facing Right` 토글 — sprite 의 기본 향. sanctum 측면 sprite 가 보통 우향이라 default true. 좌향이면 false.
   - 애니메이션 시스템 상세 = [character-animation.md](character-animation.md)
5. 자식 오브젝트 `Overhead` (Canvas, Render Mode: **WorldSpace**) 생성.
   - Canvas.scale ≈ 0.01
   - 자식: `NameText` (TMP_Text), `HostIcon` (Image), `ReadyIcon` (Image)
   - Canvas에 `LobbyPlayerOverhead.cs` 부착, 필드 3개 연결.
6. 프리팹 완성 후 Unity 상단 메뉴 `PhotonUnityNetworking/Highlight Server Settings` → `PhotonServerSettings`의 `Rpc List` 재생성 불필요하지만, 프리팹 경로는 `Resources/LobbyPlayer`여야 함.

### 4.2 LobbyPlayerEntry.prefab 생성

`Assets/Resources/Prefabs/UI/LobbyPlayerEntry.prefab`:

```
LobbyPlayerEntry  (Horizontal Layout Group, Layout Element)
├── NameText      (TMP_Text)
├── RoleText      (TMP_Text)
├── CharText      (TMP_Text)
├── ReadyText     (TMP_Text)
└── KickButton    (Button + TMP_Text "X" 또는 "강퇴")
```

- 루트에 `LobbyPlayerEntry.cs` 부착 → 인스펙터에서 5개 필드 연결.

### 4.3 Panel_Lobby.prefab 수정

> 참고: 기존 `PlayersStatusText`는 프리팹 자식이 아니라 `WaitingRoomPanelController.EnsureUiReferences()`가
> 런타임에 코드로 생성하던 임시 텍스트였다. 스크립트에서 해당 필드/생성 코드를 이미 제거했으므로
> 프리팹에서 따로 지울 것은 없다. (만약 과거 수동으로 씬에 배치해 둔 `PlayersStatusText`가 보이면 그것만 제거)

1. **신규**: `PlayersListContainer` (ScrollView 또는 그냥 `VerticalLayoutGroup`) 추가.
2. `WaitingRoomPanelController` 인스펙터 업데이트:
   - `Lobby Entry Container` ← 방금 만든 `PlayersListContainer`의 `Transform`
   - `Lobby Entry Prefab` ← `LobbyPlayerEntry.prefab`
   - `Lobby Player Spawner` ← (아래 4.4)
3. **Start 버튼** 확인:
   - `OnClick()` 리스너에서 기존 `OnClickStartOrReady` 등 Inspector 연결이 있다면 **모두 제거**. 이제 스크립트가 `OnEnable`에서 `OnClickStartGame`을 AddListener로 연결한다.
   - Inspector에 중복 연결돼 있으면 동일 핸들러가 2회 호출되므로 반드시 비워둘 것.
4. **대기실 월드 공간 셋업**:
   - Panel_Lobby 하위 또는 MenuScene 직속에 빈 GameObject `LobbyWorld` 추가.
   - 자식으로 `SpawnPoint_0` ~ `SpawnPoint_3` (Transform만) 배치.
   - 필요하면 2D 타일/Sprite로 바닥/벽 장식.

### 4.4 LobbyPlayerSpawner 배치

1. `Panel_Lobby` 하위 또는 MenuScene 어딘가에 빈 GameObject `LobbyPlayerSpawner` 생성.
2. `LobbyPlayerSpawner.cs` 부착.
3. 인스펙터:
   - `Lobby Player Prefab Name` = `LobbyPlayer` (확장자 제외, Resources 경로 이름)
   - `Spawn Points` = `SpawnPoint_0..3` 드래그
4. `WaitingRoomPanelController`의 `Lobby Player Spawner` 필드에 이 오브젝트 연결.

### 4.5 MenuSceneManager — titleBackground 연결

1. MenuScene 열기.
2. 타이틀 화면용 배경 이미지(기존 상주 GameObject. 예: `TitleBackground` / `MainBG`)를 `MenuSceneManager`의 **`Title Background`** 필드에 드래그.
3. 배경은 대기실이 활성화되면 자동으로 `SetActive(false)`, 타이틀/룸리스트로 돌아오면 true로 복원됨.

### 4.6 카메라 / 렌더링

- 대기실 월드 공간이 카메라 프러스텀 안에 들어와야 함. MenuScene 메인 카메라의 Orthographic Size/Position을 `LobbyWorld` 중심에 맞춘다.
- UI(Panel_Lobby, 카운트다운 등)는 Screen Space Overlay Canvas로 유지.
- 오버헤드 Canvas(WorldSpace)는 카메라 앞에 드러나야 하므로 Sorting Order / Layer 확인.

## 5. 런타임 흐름 (시퀀스)

```
[로컬 클라 A (Host)]
MenuScene Start
 └─ ShowTitle → (방 생성/참가)
 └─ NetworkManager.OnJoinedRoom → ShowWaitingRoom
     └─ WaitingRoomPanelController.OnEnable
         ├─ SetLocalReady(false)
         ├─ SetLocalCharacter(0)  // 최초일 때만
         ├─ lobbyPlayerSpawner.Spawn()  → PhotonNetwork.Instantiate("LobbyPlayer")
         ├─ RefreshRoomUi / RefreshRoleUi
         └─ MenuSceneManager.SetPanels → titleBackground.SetActive(false)

[플레이어 B 입장]
 └─ OnPlayerEnteredRoom → NetworkManager.PlayersInRoomChanged
     └─ A의 WaitingRoomPanelController.HandlePlayersChanged
         └─ RefreshLobbyEntries (엔트리 추가, Kick 버튼은 A만 보임)
 └─ B 클라의 Spawner가 Instantiate → A 화면에 B의 LobbyPlayer 등장

[캐릭터 변경]
 └─ B가 CharacterSelectUI 확인 → SetLocalCharacter(1)
     └─ 모든 클라의 LobbyPlayerController.OnPlayerPropertiesUpdate
         └─ SpriteRenderer 교체 + AnimatorController swap (재스폰 없음)

[강퇴]
 └─ A가 B행의 Kick 클릭 → NetworkManager.KickPlayer(B)
     └─ PhotonNetwork.CloseConnection(B) → B의 OnLeftRoom → ShowRoomList

[게임 시작]
 └─ 전원 readyToggle on
     └─ CanMasterStartGameInCurrentRoom = true → Start 버튼 interactable
 └─ A가 Start 클릭 → StartCountdown (방 프로퍼티 세팅)
     └─ 전 클라 OnRoomPropertiesUpdate → countdownText 3,2,1
     └─ 0이 되면 A만 SceneTransitionManager.EnterGameSceneByMaster() 호출
     └─ PhotonNetwork.AutomaticallySyncScene = true → 모두 GameScene 로드
     └─ GamePlayerSpawner가 characterId 읽어 인게임 플레이어 스폰
```

## 6. 엣지 케이스

- **호스트가 방을 떠남** → `OnMasterClientSwitched`에서 엔트리/역할 UI 재갱신. 새 호스트에게만 Start/Kick 권한 이양.
- **카운트다운 중 누군가 준비 해제** → `HandlePlayersChanged`가 자동으로 `CancelCountdown()`.
- **대기실에서 게임씬으로 이동할 때** → `WaitingRoomPanelController.OnDisable`의 `isLoadingGameScene` 플래그가 true면 Despawn을 건너뛴다 (씬 전환이 파괴 처리).
- **Resources 로드 실패** → LobbyPlayerSpawner가 `Resources/LobbyPlayer.prefab` 경로 확인 로그 출력.

## 7. 관련 문서

- [scene-structure.md](scene-structure.md) — MenuScene/GameScene 전체 구조
- [network-sync.md](network-sync.md) — Photon 동기화 원칙, MasterClient 권위
- [platform-integration.md](platform-integration.md) — 닉네임/사용자 ID 소스(플랫폼 SDK 경유 예정)
