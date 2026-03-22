# Skill Feature

스킬 장착, 쿨다운 관리, 스킬 시전 및 네트워크 동기화를 담당한다.

## 책임

- 스킬바(4슬롯) 장착/관리
- 시전 시 쿨다운 검증 → Delivery 전략 실행 → 이벤트 발행 → 네트워크 전파
- 원격 플레이어 스킬 수신 시 동일한 이벤트를 발행하여 로컬 연출 재생

## 이벤트 흐름

### 로컬 시전

```
Input(SlotInputHandler)
  → CastSkillUseCase.Execute(skill, casterId, time, position, direction)
    → CooldownRule 검증
    → Delivery.Deliver() → DeliveryResult 분기:
        ProjectileDeliveryResult → ProjectileRequestedEvent
        ZoneDeliveryResult      → ZoneRequestedEvent
        TargetedDeliveryResult  → TargetedRequestedEvent
        SelfDeliveryResult      → SelfRequestedEvent
    → SkillCastedEvent (UI 쿨다운용)
    → network.SendSkillCasted (RPC to Others)
```

### 원격 수신

```
SkillNetworkAdapter (RPC 수신)
  → SkillNetworkEventHandler.HandleRemoteSkillCasted
    → deliveryType에 따라 동일한 Requested 이벤트 발행
    → SkillCastedEvent 발행
    → 로컬 스포너들이 이벤트에 반응하여 이펙트/투사체 생성
```

### 이벤트 → 연출 매핑

| 이벤트 | 구독자 |
|---|---|
| `ProjectileRequestedEvent` | `ProjectileSpawner` (Projectile 피처) |
| `ZoneRequestedEvent` | `SkillCastEffectSpawner` |
| `TargetedRequestedEvent` | `SkillCastEffectSpawner` |
| `SelfRequestedEvent` | `SkillCastEffectSpawner` |
| `SkillCastedEvent` | `BarView` (UI 쿨다운) |

모든 Requested 이벤트는 `Float3 Position`, `Float3 Direction`을 포함하여
어느 플레이어 위치에서든 연출을 생성할 수 있다.

## 네트워크 동기화

- **방식**: RPC (이산 이벤트)
- **송신**: `ISkillNetworkCommandPort.SendSkillCasted(SkillCastNetworkData)`
- **수신**: `ISkillNetworkCallbackPort.OnRemoteSkillCasted`
- **전송 데이터**: skillId, casterId, deliveryType, trajectoryType, hitType, speed, radius, position, direction
- `SkillCastNetworkData` 구조체는 `ISkillNetworkCommandPort`와 같은 파일에 정의

## Bootstrap 구조

두 개의 Bootstrap 컴포넌트가 협력한다:

- **SkillBootstrap** (SkillBarCanvas 프리팹): UI + EventBus + UseCase 생성. `ConnectLocalPlayer()` / `RegisterRemotePlayer()`로 플레이어 연결 대기.
- **SkillSetup** (PlayerCharacter 프리팹): 플레이어 스폰 후 `SkillBootstrap`을 찾아 `IsMine`이면 로컬 연결, 아니면 원격 등록.

## 피처 간 의존

- **Projectile**: `ProjectileRequestedEvent`, `ProjectileSpec` 사용
- **Zone**: `ZoneView` 사용 (SkillCastEffectSpawner에서 zone 이펙트 생성)
- **Shared**: EventBus, Float3, DomainEntityId, Result

## Delivery 전략

`IDeliveryStrategy` 구현체가 시전 결과를 결정한다:

- `ProjectileDelivery` → `ProjectileDeliveryResult(ProjectileSpec)`
- `ZoneDelivery` → `ZoneDeliveryResult`
- `TargetedDelivery` → `TargetedDeliveryResult`
- `SelfDelivery` → `SelfDeliveryResult`

스킬별 Delivery 조합은 `SkillCatalog`에서 정의한다.
