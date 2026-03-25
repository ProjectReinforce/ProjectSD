# Skill Feature

스킬 입력, 쿨다운 검증, 네트워크 RPC 전송, RPC 수신 후 연출 이벤트 발행을 담당한다.

## 현재 책임

- 스킬바 4슬롯 UI 초기화와 쿨다운 표시
- 슬롯 입력 수신 (`RMB`, `Q`, `E`, `R`)
- 시전 시 쿨다운 검증 후 `SkillCastNetworkData` 전송
- RPC 수신 후 `ProjectileRequestedEvent`, `ZoneRequestedEvent`, `TargetedRequestedEvent`, `SelfRequestedEvent`, `SkillCastedEvent` 발행
- 스킬별 이펙트/사운드 매핑 (ScriptableObject 기반)

## 핵심 흐름

```text
SlotInputHandler
  -> CastSkillUseCase
    -> SkillNetworkAdapter.SendSkillCasted(RPC All)
      -> SkillNetworkAdapter.RPC_SkillCasted
        -> SkillNetworkEventHandler
          -> Requested 이벤트 발행 (SkillId 포함)
          -> SkillCastedEvent 발행
```

현재 구현은 "로컬에서도 RPC를 한 번 타고 돌아온 결과를 이벤트로 해석한다"는 방식이다.
즉 연출과 UI 쿨다운은 `SkillNetworkEventHandler`가 발행한 이벤트를 기준으로 움직인다.

## 데이터 드리븐 구조

### ScriptableObject

- `SkillData` (SO) — 스킬 하나의 전체 설정
  - Identity: `skillId` (고정 문자열, 모든 클라이언트에서 동일)
  - Spec: `damage`, `cooldown`, `range`
  - Delivery: `deliveryType` enum + Projectile 전용 필드 (`trajectoryType`, `hitType`, `speed`, `radius`)
  - Effects: `castEffectPrefab`, `castSound`
  - `ToDomain()` 메서드로 Domain `Skill` 엔티티 생성

- `SkillCatalogData` (SO) — `SkillData[]` 목록
  - Inspector에서 스킬 목록을 관리한다
  - 스킬 추가/수정 시 코드 변경 불필요

### 런타임 레지스트리

- `SkillCatalog` (클래스) — `SkillCatalogData`를 받아 런타임 딕셔너리 구성
  - `Get(skillId)` → Domain `Skill` 반환
  - `GetData(skillId)` → `SkillData` SO 반환 (이펙트/사운드 조회용)
  - 중복 ID 경고 처리

## 주요 클래스

### Bootstrap

- `SkillSetup`
  - 플레이어 프리팹에 부착되는 조립용 컴포넌트
  - `[SerializeField] SkillCatalogData`, `SkillLoadoutData`를 Inspector에서 연결
  - `Initialize(EventBus, Transform)`에서 `SkillCatalog` 생성 → `BarView`, `SkillCastEffectSpawner`, `SkillNetworkEventHandler` 초기화
  - `SkillLoadoutData` 기반 로드아웃으로 스킬바 슬롯에 장착
  - `SwapSkill(slotIndex, skillId)`: 런타임 스킬 교체 API
  - `HandleSlotClicked`: `SkillRotator`를 통한 슬롯 클릭 시 순환 교체

- `SkillIconAdapter` — `ISkillIconPort` 구현, `SkillCatalog`에서 아이콘 조회
- `SkillEffectAdapter` — `ISkillEffectPort` 구현, `SkillCatalog`에서 이펙트/사운드 조회

### Application

- `CastSkillUseCase`
  - `CooldownRule`로 시전 가능 여부 검사
  - `Delivery.Deliver()` 결과에서 `DeliveryType`을 직접 가져온다
  - `SkillCastNetworkData`를 만들어 `ISkillNetworkCommandPort`로 전송

- `EquipSkillUseCase`
  - 스킬바 슬롯에 스킬을 장착한다
  - 기존 스킬의 쿨다운을 `CooldownTracker`에서 제거한다
  - `SkillEquippedEvent`를 발행한다
  - `BuildFromLoadout()`: 로드아웃 기반 초기 스킬바 조립

- `SkillRotator`
  - 카탈로그 스킬 ID 목록을 순환하며 다음 스킬을 반환한다
  - 현재 슬롯에 장착된 스킬을 건너뛴다

- `SkillNetworkEventHandler`
  - `SkillCastNetworkData`를 받아 이벤트 버스로 변환한다
  - `DeliveryType` enum에 따라 아래 이벤트를 발행한다
    - `ProjectileRequestedEvent`
    - `ZoneRequestedEvent` (SkillId 포함)
    - `TargetedRequestedEvent` (SkillId 포함)
    - `SelfRequestedEvent` (SkillId 포함)
  - 마지막에 `SkillCastedEvent`를 발행한다

- `Ports/ISkillNetworkCommandPort`, `ISkillNetworkCallbackPort` — 네트워크 송수신 포트
- `Ports/ISkillLoadoutRepository` — 로드아웃 저장/로드 포트

### Infrastructure

- `SkillNetworkAdapter` (`MonoBehaviourPun`) — RPC 송수신
- `SkillLoadoutRepository` — `SkillLoadoutData` SO 기반 로드아웃 로드
- `SkillLoadoutData` (SO) — Inspector에서 슬롯별 스킬 ID 설정

### Presentation

- `ISkillIconPort` — 스킬 아이콘(Sprite) 조회 포트 (Unity 타입이므로 Presentation에 배치)
- `ISkillEffectPort` — 스킬 이펙트(GameObject)/사운드(AudioClip) 조회 포트

- `SlotInputHandler`
  - 입력 액션을 바인딩하고 시전 요청을 보낸다
  - `SetPlayerTransform()`으로 전달받은 플레이어 트랜스폼의 위치/전방을 시전 origin으로 사용한다

- `BarView` — `SkillEquippedEvent`, `SkillCastedEvent` 구독, 슬롯 쿨다운/아이콘 표시
- `SlotView` — 개별 슬롯 UI 렌더링
- `SkillCastEffectSpawner` — 이벤트의 SkillId로 이펙트 프리팹/사운드 재생
- `SelfCastEffect`, `TargetedCastEffect` — 캐스트 이펙트 연출

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
- Inspector에서 `SkillCatalogData`를 연결해야 한다 — 없으면 초기화 중단

## 스킬 추가 방법

1. Unity에서 `Create > Skill > SkillData`로 SO 생성
2. Inspector에서 ID, 스펙, 딜리버리, 이펙트/사운드 설정
3. `SkillCatalogData`의 `Skills` 배열에 추가
4. 코드 변경 불필요

## 피처 간 의존

- `Projectile`: `ProjectileRequestedEvent`, `ProjectileSpec`
- `Zone`: `ZoneRequestedEvent` (Skill이 발행, Zone이 구독)
- `Shared`: `EventBus`, `DomainEntityId`, `Result`, `Float3`

## 현재 문서 범위

이 문서는 현재 코드 구현을 기준으로 작성되었다.
설계 의도나 이후 리팩터링 방향이 아니라, 지금 실제로 존재하는 조립 경로와 책임만 기록한다.
