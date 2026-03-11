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
- Cross-feature dependency is allowed as long as layer direction is respected (same-or-inner layer only).
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

## Anti-Patterns (Never Do)

- Business logic inside View or InputHandler
- Networking or Unity API inside Domain
- Feature-specific code inside Shared
- Bootstrap becoming a god class
- One port handling unrelated responsibilities
- Layers not defined in architecture.md
- Silent failure on null — use `Debug.LogError` for missing SerializeField/injected dependencies; do not add null checks for internal data (let NullReferenceException surface naturally)
- Behavioral switch on type enums — use Factory + Strategy pattern instead; switch is fine for command dispatch and simple value mapping

**When unsure:** keep code inside the current feature rather than moving it to Shared.

---

## Rule Priority (on conflict)

1. `dependency_rules.md`
2. `layer_rules.md`
3. `architecture.md`
4. `feature_rules.md`
5. `naming_rules.md`
6. `anti_patterns.md`
