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

## Network Feature Standard

네트워크 동기화가 필요한 피처는 아래 구조를 따른다.

### Application Layer

| 클래스 | 역할 |
|---|---|
| `<Feature>UseCases` | 로컬 액션 → 검증 → 네트워크 명령 전송 |
| `<Feature>NetworkEventHandler` | 네트워크 콜백 수신 → 도메인 상태 갱신 → 이벤트 발행 |

### Port Interfaces (Application/Ports)

| 포트 | 역할 |
|---|---|
| `I<Feature>NetworkCommandPort` | 네트워크 명령 전송 (RPC 등) |
| `I<Feature>NetworkCallbackPort` | 네트워크 콜백 수신 |

### Infrastructure Layer

| 클래스 | 역할 |
|---|---|
| `<Feature>NetworkAdapter` | CommandPort + CallbackPort 구현 |

### 동기화 방식

모든 네트워크 데이터는 반드시 아래 세 경로 중 하나를 통해야 한다.

- **연속 데이터** → `OnPhotonSerializeView` (위치, 회전 등 매 프레임 변경되는 값)
- **이산 이벤트** → `RPC` (점프, 스킬 캐스팅, 데미지 등 특정 시점에 발생하는 이벤트)
- **상태 동기화** → `CustomProperties` (팀, 레디, 닉네임 등 현재 상태를 나타내는 값. 늦게 입장한 유저에게 자동 동기화됨)

다른 동기화 방식(PhotonTransformView 등)은 사용하지 않는다.

### Port 정리 원칙

- 네트워크 포트는 **방향별**로 나눈다: `CommandPort` (보내기), `CallbackPort` (받기)
- 포트의 반환 타입/데이터 클래스는 해당 포트와 **같은 파일**에 둔다

### Bootstrap 조립 순서

1. `NetworkAdapter` 참조 획득 (프리팹에서 GetComponent)
2. `NetworkEventHandler` 생성 (콜백 포트 연결)
3. `UseCases` 생성 (커맨드 포트 + 기타 의존성 주입)
4. Presentation 초기화

---

## Naming Conventions

- **Entity**: no suffix — `Lobby`, `Room`, `RoomMember`
- **UseCases**: `LobbyUseCases`, `PlayerUseCases` (피처당 하나로 통합)
- **NetworkEventHandler**: `LobbyNetworkEventHandler`, `PlayerNetworkEventHandler`
- **Port interface**: `ILobbyRepository`, `IPlayerNetworkCommandPort`, `IPlayerNetworkCallbackPort`
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
