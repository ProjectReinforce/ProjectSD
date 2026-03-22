# Lobby Feature

멀티플레이 로비 기능. 방 생성/입장/퇴장, 팀 변경, 레디, 게임 시작을 담당한다.

## 책임

- 방 목록 조회 및 표시
- 방 생성/입장/퇴장
- 방 내 팀 변경, 레디 상태 토글
- 게임 시작 조건 검증 및 시작 트리거

## 이벤트 흐름

### 명령 경로 (로컬 → 네트워크)

```
LobbyView (UI 입력)
  → LobbyUseCases.CreateRoom / JoinRoom / LeaveRoom / ChangeTeam / SetReady / StartGame
    → ILobbyRepository로 현재 상태 검증
    → ILobbyNetworkCommandPort (LobbyPhotonAdapter)
      → Photon API 호출 (CreateRoom, CustomProperties 등)
```

### 콜백 경로 (네트워크 → UI)

```
Photon 콜백 (OnCreatedRoom, OnJoinedRoom, OnPlayerEnteredRoom 등)
  → LobbyPhotonAdapter → ILobbyNetworkCallbackPort Action 호출
    → LobbyNetworkEventHandler
      → ILobbyRepository 업데이트 (도메인 상태 반영)
      → EventBus 이벤트 발행:
          LobbyUpdatedEvent, RoomUpdatedEvent, GameStartedEvent 등
        → LobbyView가 구독하여 UI 갱신
```

### 핵심 설계: 이벤트 드리븐

UseCase는 커맨드만 발사하고 끝낸다.
Photon 콜백 해석과 도메인 상태 업데이트는 `LobbyNetworkEventHandler`가 처리한다.

### 에러 처리 분리

| 에러 종류 | 처리 방식 |
|---|---|
| 동기 유효성 에러 (방 이름 중복 등) | UseCase가 `Result.Failure` 반환 → View에서 즉시 처리 |
| 비동기 네트워크 에러 (방 입장 실패 등) | `LobbyNetworkEventHandler`가 `LobbyErrorEvent` 발행 → View가 구독 |

## 네트워크 동기화

| 데이터 | 방식 | 용도 |
|---|---|---|
| 팀, 레디, 닉네임 | `CustomProperties` (상태 동기화) | 늦게 입장한 유저에게 자동 동기화 |
| 게임 시작 | `RaiseEvent` (이산 이벤트) | 전체 방 알림 |
| 방 입퇴장 | Photon 자체 콜백 | 멤버 변동 감지 |

`LobbyPhotonAdapter`가 `ILobbyNetworkCommandPort`(송신)와 `ILobbyNetworkCallbackPort`(수신)을 모두 구현한다.

## 도메인 모델

- **Lobby**: 방 컬렉션 관리 (aggregate root)
- **Room**: 멤버 관리, 팀/레디 상태
- **RoomMember**: Id, DisplayName, Team, IsReady
- **LobbyRule**: 방 생성/게임 시작 조건 검증

주의: `Lobby` 클래스명이 `Features.Lobby` 네임스페이스와 충돌하므로
다른 레이어에서 사용 시 `using DomainLobby = Features.Lobby.Domain.Lobby;` alias 필요.

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

## Bootstrap

- **LobbyBootstrap** (씬 오브젝트): LobbyPhotonAdapter → LobbyNetworkEventHandler → LobbyUseCases → LobbyView 순서로 조립

## 피처 간 의존

- **독립적**: 다른 피처에 의존하지 않음
- **Shared**: EventBus, DomainEntityId, Result, IClockPort
