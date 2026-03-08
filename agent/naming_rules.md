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

Avoid overly generic names.

---

## Event Naming

Domain events use past-tense + Event suffix.

Examples:

LobbyUpdatedEvent
RoomUpdatedEvent
GameStartedEvent
LobbyErrorEvent

---

## EventBus Naming

IEventBus
EventBus

Location: Shared/EventBus/

---

## Adapter Naming

Infrastructure implementations should use Adapter suffix.

Examples:

LobbyPhotonAdapter
ClockAdapter

---

## UI Naming

Views:

LobbyView
RoomListView
RoomDetailView

InputHandler:

LobbyInputHandler
