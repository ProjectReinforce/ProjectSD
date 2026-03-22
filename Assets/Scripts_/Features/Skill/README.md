# Skill Feature

스킬 입력, 쿨다운 검증, 네트워크 RPC 전송, RPC 수신 후 연출 이벤트 발행을 담당한다.

## 현재 책임

- 스킬바 4슬롯 UI 초기화와 쿨다운 표시
- 슬롯 입력 수신 (`RMB`, `Q`, `E`, `R`)
- 시전 시 쿨다운 검증 후 `SkillCastNetworkData` 전송
- RPC 수신 후 `ProjectileRequestedEvent`, `ZoneRequestedEvent`, `TargetedRequestedEvent`, `SelfRequestedEvent`, `SkillCastedEvent` 발행

## 핵심 흐름

```text
SlotInputHandler
  -> CastSkillUseCase
    -> SkillNetworkAdapter.SendSkillCasted(RPC All)
      -> SkillNetworkAdapter.RPC_SkillCasted
        -> SkillNetworkEventHandler
          -> Requested 이벤트 발행
          -> SkillCastedEvent 발행
```

현재 구현은 "로컬에서도 RPC를 한 번 타고 돌아온 결과를 이벤트로 해석한다"는 방식이다.
즉 연출과 UI 쿨다운은 `SkillNetworkEventHandler`가 발행한 이벤트를 기준으로 움직인다.

## 주요 클래스

### Bootstrap

- `SkillSetup`
  - 플레이어 프리팹에 부착되는 조립용 컴포넌트
  - `Initialize(EventBus, Transform playerTransform)`에서 `BarView`, `SkillCastEffectSpawner`, `SkillNetworkEventHandler`를 초기화한다
  - 스킬바 생성, `CastSkillUseCase` 생성, `SlotInputHandler.Initialize()` 호출, `SetPlayerTransform(playerTransform)`까지 담당한다
  - `_slotInputHandler`가 Inspector에서 연결되지 않으면 `Debug.LogError`로 알리고 중단한다

- `SkillCatalog`
  - 정적 팩토리로 스킬 인스턴스를 생성한다
  - 각 스킬은 고정 ID(`"skill-fireball"` 등)를 가진다 — 쿨다운 추적, 네트워크 동기화 시 ID 일관성 보장

### Application

- `CastSkillUseCase`
  - `CooldownRule`로 시전 가능 여부 검사
  - `Delivery.Deliver()` 결과에서 `DeliveryType`을 직접 가져온다
  - `SkillCastNetworkData`를 만들어 `ISkillNetworkCommandPort`로 전송
  - `_network`는 생성자에서 필수 주입한다

- `SkillNetworkEventHandler`
  - `SkillCastNetworkData`를 받아 이벤트 버스로 변환한다
  - `DeliveryType` enum에 따라 아래 이벤트를 발행한다
    - `ProjectileRequestedEvent`
    - `ZoneRequestedEvent`
    - `TargetedRequestedEvent`
    - `SelfRequestedEvent`
  - 마지막에 `SkillCastedEvent`를 발행한다

### Infrastructure

- `SkillNetworkAdapter`
  - `MonoBehaviourPun`
  - `SkillCastNetworkData`를 개별 파라미터로 분해해 `RpcTarget.All`로 전송한다
  - RPC 수신 시 개별 파라미터를 다시 `SkillCastNetworkData`로 복원해 콜백으로 넘긴다

### Presentation

- `SlotInputHandler`
  - 입력 액션을 바인딩하고 시전 요청을 보낸다
  - `SetPlayerTransform()`으로 전달받은 플레이어 트랜스폼의 위치/전방을 시전 origin으로 사용한다
  - 플레이어 트랜스폼이 null이면 자기 자신의 `transform`을 fallback으로 사용한다

- `BarView`
  - `SkillEquippedEvent`, `SkillCastedEvent`를 구독한다
  - 현재는 `SkillCastedEvent.SlotIndex`를 기준으로 해당 슬롯 쿨다운만 시작한다

- `SkillCastEffectSpawner`
  - `ZoneRequestedEvent`, `TargetedRequestedEvent`, `SelfRequestedEvent`를 구독한다
  - 필요한 프리팹이 비어 있으면 `Resources`에서 기본 프리팹을 로드한다

## 네트워크 데이터

`SkillCastNetworkData`는 다음 정보를 담는다.

- `SkillId`, `CasterId`, `SlotIndex`
- `Damage`, `Cooldown`, `Range`
- `DeliveryType` (enum)
- `TrajectoryType`, `HitType`
- `Speed`, `Radius`
- `Position` (Float3), `Direction` (Float3)

Position/Direction에 `Float3`를 사용해 XYZ를 묶었다.
RPC 전송 시 Infrastructure에서 개별 float로 분해하고, 수신 시 다시 `Float3`로 조립한다.

## JG_GameScene 기준 조립 상태

`JG_GameScene`에는 다음이 배치되어 있다.

- `SkillBarCanvas` 프리팹 인스턴스
- 씬 오브젝트 `GameSceneBootstrap`
- `GameSceneBootstrap._skillSetup` 필드에 `SkillSetup` 참조
- `GameSceneBootstrap._projectileSpawner` 필드에 `ProjectileSpawner` 참조
- `_playerPrefabName = PlayerCharacter`

코드 기준 실제 연결은 아래와 같다.

- `GameSceneBootstrap.Start()`
  - 플레이어를 `PhotonNetwork.Instantiate()`로 생성
  - `ConnectPlayer()`: 생성된 플레이어에서 `PlayerSetup.Initialize(eventBus)` 호출
  - `_skillSetup.Initialize(_eventBus, player.transform)`: 스킬 시스템 초기화 및 플레이어 트랜스폼 전달
  - `_projectileSpawner.Initialize(_eventBus, _eventBus)`: 투사체 스포너 초기화
  - 기존 원격 플레이어에 대해 `ConnectRemotePlayerDelayed()` 호출

## 현재 코드 기준 주의점

- `GameSceneBootstrap`이 `SkillSetup.Initialize(_eventBus, player.transform)`를 호출하므로 스킬 시스템은 정상 초기화된다
- `SkillSetup.Initialize()`가 호출되지 않으면 입력 바인딩이 없어 스킬 사용 불가

## 피처 간 의존

- `Projectile`
  - `ProjectileRequestedEvent`
  - `ProjectileSpec`
  - `ProjectileSpawner`

- `Zone`
  - `ZoneView`

- `Shared`
  - `EventBus`
  - `DomainEntityId`
  - `Result`
  - `Float3`

## 현재 문서 범위

이 문서는 현재 코드 구현을 기준으로 작성되었다.
설계 의도나 이후 리팩터링 방향이 아니라, 지금 실제로 존재하는 조립 경로와 책임만 기록한다.
