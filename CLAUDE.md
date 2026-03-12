# CLAUDE.md

This project follows **Feature-first Clean Architecture**.
Refer to the `/agent` directory for detailed rules.

---

## Architecture

```
Features/<FeatureName>/
  Domain/
  Application/
  Presentation/
  Infrastructure/
  Bootstrap/
Shared/
```

- Each feature is self-contained and grows independently.
- When investigating files for a feature, first read `Assets/Scripts_/Features/<FeatureName>/README.md` if it exists.
- `Shared` contains only reusable cross-feature utilities — never feature-specific code.
- Cross-feature dependency is encouraged — layer direction만 지키면 피처 간 적극적으로 의존한다.
- Only split a feature into two when a concept gains an independent lifecycle.

---

## Dependency Direction

```
Presentation -> Application -> Domain
Infrastructure -> Application
Shared -> (no feature dependency)
```

- `Domain`: no Unity API, no Photon API, no IO, no database.
- `Application`: depends on Domain, Shared, and other features' Application or Domain.
- `Presentation`: depends on Application, Domain, Shared, and other features' same-or-inner layers.
- `Infrastructure`: depends on Application, Domain, Shared, and other features' same-or-inner layers; implements Application ports; no business logic.

---

## Layer Responsibilities

| Layer | Contains | Must NOT contain |
|---|---|---|
| Domain | Entities, ValueObjects, business rules | Unity/Photon API, IO, UI |
| Application | UseCases, port interfaces, events | Business rules, Unity API |
| Presentation | View, InputHandler | Business logic |
| Infrastructure | Photon/DB adapters, external SDKs | Business logic |
| Bootstrap | Composition and wiring between layers | Business logic, rendering |

- UseCases must remain thin — coordinate domain logic, not contain it.
- Wiring and composition across a feature's layers must live in that feature's Bootstrap.

---

## Naming Conventions

- **Entity**: no suffix — `Lobby`, `Room`, `RoomMember`
- **UseCase**: `CreateRoomUseCase`, `JoinRoomUseCase`, `LeaveRoomUseCase`
- **Port interface**: `ILobbyRepository`, `ILobbyNetworkPort`
- **Event**: `LobbyUpdatedEvent`, `RoomUpdatedEvent`, `GameStartedEvent`
- **EventBus**: `IEventBus`, `EventBus` (in `Shared/EventBus/`)
- **Adapter**: `LobbyPhotonAdapter`, `ClockAdapter`
- **View**: `LobbyView`, `RoomListView`, `RoomDetailView`

---

## Design Principles

- Colocate code that changes for the same reason — if a single requirement change forces edits across multiple classes or files, those pieces belong together. Conversely, if one class changes for multiple unrelated reasons, split it.
- Minimize the ripple effect of changes — a class exposes only what it does (interface), never how it does it (implementation). If changing an implementation forces callers to change too, the boundary is wrong.



---

## Agent Reasoning

- Do not jump to yes/no — first check project rules and current code, then answer based on evidence.
- Do not start with the conclusion and retrofit the reasoning.
- If a user request conflicts with or shows signs of violating Design Principles, flag it before proceeding.

---

## Rule Priority (on conflict)

1. `dependency_rules.md`
2. `layer_rules.md`
3. `architecture.md`
4. `feature_rules.md`
5. `naming_rules.md`
6. `anti_patterns.md`
