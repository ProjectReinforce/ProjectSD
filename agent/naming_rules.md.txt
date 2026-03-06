# /agent/naming_rules.md

## Entity Naming

Entities should have no suffix.

Examples:

Lobby
Room
RoomMember

---

## Use Case Naming

Use:

CreateRoomUseCase
JoinRoomUseCase
LeaveRoomUseCase
ChangeTeamUseCase
SetReadyUseCase

---

## Port Naming

Interfaces must use clear feature context.

Examples:

ILobbyRepository
ILobbyNetworkPort
ILobbyOutputPort

Avoid overly generic names.

---

## Adapter Naming

Infrastructure implementations should use Adapter suffix.

Examples:

LobbyPhotonAdapter
ClockAdapter

---

## UI Naming

Presenter:

LobbyPresenter

Views:

LobbyView
RoomListView
RoomDetailView
