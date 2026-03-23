# Zone Feature

지속/범위형 스킬의 시각 영역을 스폰하고 수명 동안 유지하는 피처다.

## 현재 책임

- Skill 피처가 발행한 `ZoneRequestedEvent`를 해석한다.
- Zone 도메인 엔티티를 생성한다.
- Zone 프리팹 스폰과 수명 관리를 Zone 피처 내부에서 수행한다.

## 목표 구조

```text
Skill ZoneRequestedEvent
  -> ZoneSetup
    -> SpawnZoneUseCase
      -> IZoneEffectPort
        -> ZoneEffectAdapter
          -> ZoneView
```

## 레이어 메모

- **Domain**: `Zone`, `ZoneSpec`, anchor/hit enum
- **Application**: `SpawnZoneUseCase`, `IZoneEffectPort`, zone events
- **Infrastructure**: `IZoneEffectPort` 구현체로 Unity 프리팹 스폰 담당
- **Bootstrap**: Skill 이벤트를 받아 UseCase로 연결
- **Presentation**: 실제 이펙트 표현과 수명 관리

## 현재 구현 기준 결정

- Zone은 별도 네트워크 어댑터를 두지 않는다.
- Skill 피처가 이미 RPC 이후 `ZoneRequestedEvent`를 각 클라이언트에서 발행하므로,
  Zone은 그 이벤트를 받아 로컬 시각 연출을 만든다.
- 위치는 Skill 이벤트의 `position + direction * (range * 0.5)` 규칙을 따른다.

## 피처 간 의존

- **Skill**: `ZoneRequestedEvent`
- **Shared**: `EventBus`, `Result`, `Float3`, `IClockPort`, `DomainEntityId`
