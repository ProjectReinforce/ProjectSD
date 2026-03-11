# Lobby Feature

멀티플레이 로비 기능. 방 생성/입장/퇴장, 팀 변경, 레디, 게임 시작을 담당한다.

---

## 디렉토리 구조

```
Lobby/
├── Domain/
│   ├── Lobby/
│   │   ├── Lobby.cs           - 방 목록 관리 (AddRoom, RemoveRoom, FindRoom)
│   │   └── LobbyRule.cs       - 비즈니스 규칙 (방 이름 검증, 게임 시작 조건)
│   └── Room/
│       ├── Room.cs            - 방 엔티티 (멤버 관리, 팀/레디 변경)
│       ├── RoomMember.cs      - 멤버 엔티티 (이름, 팀, 레디 상태)
│       └── TeamType.cs        - 팀 열거형
│
├── Application/
│   ├── Ports/
│   │   ├── ILobbyRepository.cs   - 로컬 상태 저장소 포트
│   │   └── ILobbyNetworkPort.cs  - 네트워크 통신 포트
│   ├── Events/
│   │   ├── LobbyUpdatedEvent.cs  - 로비 전체 상태 변경
│   │   ├── RoomUpdatedEvent.cs   - 특정 방 상태 변경
│   │   ├── GameStartedEvent.cs   - 게임 시작
│   │   ├── LobbyErrorEvent.cs    - 비동기 에러 알림
│   │   ├── LobbySnapshot.cs      - 이벤트용 로비 스냅샷 (불변)
│   │   └── RoomSnapshot.cs       - 이벤트용 방/멤버 스냅샷 (불변)
│   ├── CreateRoomUseCase.cs
│   ├── JoinRoomUseCase.cs
│   ├── LeaveRoomUseCase.cs
│   ├── ChangeTeamUseCase.cs
│   ├── SetReadyUseCase.cs
│   └── StartGameUseCase.cs
│
├── Infrastructure/
│   ├── Persistence/
│   │   └── LobbyRepository.cs         - 인메모리 로컬 상태 저장소
│   └── Photon/
│       ├── LobbyPhotonAdapter.cs       - ILobbyNetworkPort 구현, 커맨드 발사
│       ├── PhotonNetworkEventHandler.cs - Photon 콜백 수신 → 도메인 업데이트
│       ├── PhotonPlayerPropertyManager.cs - Photon CustomProperties 읽기/쓰기
│       └── LobbyPhotonConstants.cs     - Photon key 상수
│
├── Presentation/
│   ├── LobbyView.cs      - 이벤트 구독, 하위 View에 위임
│   ├── RoomListView.cs   - 방 목록 렌더링
│   └── RoomDetailView.cs - 방 상세 (멤버 목록, 팀, 레디, 게임 시작)
│
└── Bootstrap/
    └── LobbyBootstrap.cs - 의존성 주입 및 초기화
```

---

## 레이어 의존 방향

```
Presentation
    │  이벤트 구독 (LobbyUpdatedEvent, RoomUpdatedEvent, ...)
    │  UseCase 직접 호출 (동기 결과는 Result로 받음)
    ▼
Application  ──────────────────────────────────────────────────────────
    │  포트 호출 (ILobbyRepository, ILobbyNetworkPort)               │
    │  비즈니스 규칙은 Domain에 위임                                   │
    ▼                                                               │
Domain                                                              │
    Lobby, Room, RoomMember, LobbyRule                              │
                                                                    │
Infrastructure ──────────────────────────────────────────────────────
    ILobbyRepository  ← LobbyRepository (인메모리)
    ILobbyNetworkPort ← LobbyPhotonAdapter (Photon PUN2)
```

---

## 핵심 설계: Photon 이벤트 드리븐

> UseCase는 커맨드만 발사하고 끝낸다.
> 도메인 상태 업데이트와 이벤트 발행은 `PhotonNetworkEventHandler`의 Photon 콜백이 처리한다.

### 에러 처리 분리

| 에러 종류 | 처리 방식 |
|---|---|
| 동기 유효성 에러 (방 이름 중복 등) | UseCase가 `Result.Failure` 반환 → View에서 즉시 처리 |
| 비동기 네트워크 에러 (방 입장 실패 등) | `PhotonNetworkEventHandler`가 `LobbyErrorEvent` 발행 → View가 구독하여 처리 |

---

## 주요 흐름

### 1. 방 생성 (CreateRoom)

```
[Host 클라이언트]

LobbyView.OnCreateClick()
  └─ CreateRoomUseCase.Execute(name, capacity, ownerName)
       ├─ LobbyRule.ValidateRoomName()        ← 실패 시 즉시 Result.Failure 반환
       ├─ LobbyRule.EnsureUniqueRoomName()
       ├─ Room.Create(id, name, capacity, owner)
       └─ ILobbyNetworkPort.CreateRoom(room)
            └─ LobbyPhotonAdapter
                 ├─ 유효성 검사 (연결 상태, 이미 방에 있는지)
                 ├─ SetLocalMemberProperties(owner)  → Photon CustomProperties 설정
                 ├─ EventHandler.SetPendingCreate(room)
                 └─ PhotonNetwork.CreateRoom(...)

                               ↓ Photon 서버 응답

PhotonNetworkEventHandler.OnCreatedRoom()
  └─ lobby.AddRoom(pendingCreateRoom)
  └─ repository.SaveLobby(lobby)
  └─ Publish(LobbyUpdatedEvent)
  └─ Publish(RoomUpdatedEvent)

PhotonNetworkEventHandler.OnJoinedRoom()   ← 방 만든 사람도 수신
  └─ _pendingJoin == false → return (무시, 위에서 이미 처리)
```

---

### 2. 방 입장 (JoinRoom)

```
[Guest 클라이언트]                       [기존 방 멤버들]

LobbyView.OnJoinClick(roomId)
  └─ JoinRoomUseCase.Execute(roomId, name)
       ├─ RoomMember 생성 (새 EntityId)
       └─ ILobbyNetworkPort.JoinRoom(roomId, member)
            └─ LobbyPhotonAdapter
                 ├─ SetLocalMemberProperties(member)
                 ├─ EventHandler.SetPendingJoin()
                 └─ PhotonNetwork.JoinRoom(roomId)

                ↓ Photon 서버 응답            ↓ Photon 푸시

OnJoinedRoom()                          OnPlayerEnteredRoom(newPlayer)
  └─ PhotonNetwork.CurrentRoom에서         └─ BuildMemberFromPlayer(player)
     전체 멤버 포함 Room 재구성               └─ room.AddMember(member)
  └─ lobby.AddRoom(room)                  └─ repository.SaveLobby(lobby)
  └─ repository.SaveLobby(lobby)          └─ Publish(RoomUpdatedEvent)
  └─ Publish(LobbyUpdatedEvent)
  └─ Publish(RoomUpdatedEvent)
```

> 핵심: Guest의 `OnJoinedRoom`에서 `PhotonNetwork.CurrentRoom`으로 방 전체 상태를 복원한다.
> 기존 멤버들은 `OnPlayerEnteredRoom`으로 신규 멤버를 받아 도메인에 추가한다.

---

### 3. 방 퇴장 (LeaveRoom)

```
LeaveRoomUseCase.Execute(roomId, memberId)
  ├─ 로컬 room/member 존재 확인 (동기 검증)
  └─ ILobbyNetworkPort.LeaveRoom(roomId, memberId)
       └─ LobbyPhotonAdapter
            ├─ EventHandler.SetPendingLeave(roomId, memberId)  ← 나간 후엔 CurrentRoom이 null
            └─ PhotonNetwork.LeaveRoom()

                               ↓ Photon 서버 응답

PhotonNetworkEventHandler.OnLeftRoom()
  └─ pendingLeaveRoomId/memberId 사용
  └─ room.RemoveMember(memberId)
  └─ 빈 방이면 lobby.RemoveRoom(roomId)
  └─ repository.SaveLobby(lobby)
  └─ Publish(LobbyUpdatedEvent)
  └─ Publish(RoomUpdatedEvent)   ← 멤버가 남아있을 때만
```

> `SetPendingLeave`가 필요한 이유: `OnLeftRoom` 시점에는 이미 방을 나간 상태라
> `PhotonNetwork.CurrentRoom == null`이기 때문.

---

### 4. 팀 변경 / 레디 (ChangeTeam / SetReady)

```
ChangeTeamUseCase / SetReadyUseCase
  ├─ 로컬 room/member 존재 확인
  └─ ILobbyNetworkPort.ChangeTeam(memberId, team) / SetReady(memberId, isReady)
       └─ LobbyPhotonAdapter
            ├─ 로컬 플레이어 검증 (자기 자신만 변경 가능)
            └─ PhotonNetwork.LocalPlayer.SetCustomProperties(...)

                               ↓ Photon 서버 응답 (전체 클라이언트)

PhotonNetworkEventHandler.OnPlayerPropertiesUpdate(player, changedProps)
  └─ room.ChangeTeam(memberId, team) / room.SetReady(memberId, isReady)
  └─ repository.SaveLobby(lobby)
  └─ Publish(RoomUpdatedEvent)
```

> 로컬/원격 플레이어 모두 동일한 콜백으로 처리한다.
> 자기 자신도 서버를 통해 확인받은 뒤 도메인에 반영된다.

---

### 5. 게임 시작 (StartGame)

```
[방장만 호출 가능]

StartGameUseCase.Execute(roomId)
  ├─ LobbyRule.CanStartGame() → 인원 2명 이상 + 전원 레디
  └─ ILobbyNetworkPort.StartGame(roomId)
       └─ LobbyPhotonAdapter
            ├─ MasterClient 여부 확인
            ├─ PhotonNetwork.RaiseEvent(GameStartedEventCode, roomId, ReceiverGroup.All)
            └─ PhotonNetwork.LoadLevel("GameScene")

                               ↓ 전체 클라이언트 수신

PhotonNetworkEventHandler.OnEvent(photonEvent)
  └─ GameStartedEventCode 확인
  └─ Publish(GameStartedEvent(room))
     → LobbyView.RenderStartGame()
```

---

## 이벤트 목록

| 이벤트 | 발행 시점 | 구독자 |
|---|---|---|
| `LobbyUpdatedEvent` | 방 추가/삭제 | `LobbyView` → `RoomListView` |
| `RoomUpdatedEvent` | 멤버 변경, 팀/레디 변경 | `LobbyView` → `RoomDetailView` |
| `GameStartedEvent` | 게임 시작 RaiseEvent 수신 | `LobbyView` |
| `LobbyErrorEvent` | 비동기 네트워크 실패 | `LobbyView` |

---

## 도메인 모델

```
Lobby
 └─ rooms: List<Room>

Room (Entity)
 ├─ Id: EntityId          (= Photon Room Name)
 ├─ Name: string
 ├─ Capacity: int
 ├─ OwnerId: EntityId     (MasterClient의 도메인 멤버 ID)
 └─ members: List<RoomMember>

RoomMember (Entity)
 ├─ Id: EntityId          (= Photon CustomProperties["memberId"])
 ├─ DisplayName: string
 ├─ Team: TeamType
 └─ IsReady: bool
```

---

## Photon ↔ 도메인 매핑

| Photon | 도메인 |
|---|---|
| `PhotonNetwork.CurrentRoom.Name` | `Room.Id.Value` |
| `CustomProperties["roomDisplayName"]` | `Room.Name` |
| `Room.MaxPlayers` | `Room.Capacity` |
| `MasterClientId` | `Room.OwnerId` |
| `Player.CustomProperties["memberId"]` | `RoomMember.Id.Value` |
| `Player.CustomProperties["displayName"]` | `RoomMember.DisplayName` |
| `Player.CustomProperties["team"]` | `RoomMember.Team` |
| `Player.CustomProperties["isReady"]` | `RoomMember.IsReady` |

---

## 스냅샷 (Snapshot)

이벤트에는 도메인 객체 참조를 직접 넘기지 않고 **불변 스냅샷**을 사용한다.

```
Room (mutable entity)
  → RoomSnapshot (readonly struct, 이벤트에 담김)
       └─ RoomMemberSnapshot[]

Lobby
  → LobbySnapshot (readonly struct)
       └─ RoomSnapshot[]
```

View는 항상 스냅샷을 통해 데이터를 읽는다. 도메인 객체를 직접 보유하지 않는다.
