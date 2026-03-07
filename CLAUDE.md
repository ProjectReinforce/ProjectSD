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
Shared/
```

- Each feature is self-contained.
- `Shared` contains only reusable cross-feature utilities — never feature-specific code.
- Features communicate via ports or events, never by directly referencing each other's internals.

---

## Dependency Direction

```
Application -> Domain
Application -> Shared
Presentation -> Application
Presentation -> Domain
Presentation -> Shared
Infrastructure -> Application
Infrastructure -> Domain
Infrastructure -> Shared
Shared -> (no feature dependency)
```

- `Domain`: no Unity API, no Photon API, no IO, no database.
- `Application`: depends on Domain and Shared only.
- `Presentation`: depends on Application, Domain and Shared only.
- `Infrastructure`: depends on Application, Domain and Shared only; implements Application ports; no business logic.

---

## Layer Responsibilities

| Layer | Contains | Must NOT contain |
|---|---|---|
| Domain | Entities, ValueObjects, business rules | Unity/Photon API, IO, UI |
| Application | UseCases, port interfaces | Business rules, Unity API |
| Presentation | EntryPoint, Presenter, View, InputHandler | Business logic |
| Infrastructure | Photon/DB adapters, external SDKs | Business logic |

- UseCases must remain thin — coordinate domain logic, not contain it.

---

## Naming Conventions

- **Entity**: no suffix — `Lobby`, `Room`, `RoomMember`
- **UseCase**: `CreateRoomUseCase`, `JoinRoomUseCase`, `LeaveRoomUseCase`
- **Port interface**: `ILobbyRepository`, `ILobbyNetworkPort`, `ILobbyOutputPort`
- **Adapter**: `LobbyPhotonAdapter`, `ClockAdapter`
- **Presenter**: `LobbyPresenter`
- **View**: `LobbyView`, `RoomListView`, `RoomDetailView`

---

## Anti-Patterns (Never Do)

- Business logic inside Presenter
- Networking or Unity API inside Domain
- Feature-specific code inside Shared
- Generic abstractions without real duplication
- EntryPoint becoming a god class
- One port handling unrelated responsibilities
- Layers not defined in architecture.md

**When unsure:** keep code inside the current feature rather than moving it to Shared.

---

## Rule Priority (on conflict)

1. `dependency_rules.md`
2. `layer_rules.md`
3. `architecture.md`
4. `feature_rules.md`
5. `naming_rules.md`
6. `anti_patterns.md`
