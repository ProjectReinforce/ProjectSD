# Combat Feature

대상 방어력 기반 데미지 계산과 데미지 적용 이벤트 발행을 담당한다.

## 현재 책임

- `DamageRule`로 최종 데미지를 계산한다.
- 타깃 포트를 통해 데미지를 적용한다.
- `DamageAppliedEvent`를 발행한다.

## 목표 구조

```text
Caller
  -> CombatBootstrap.ApplyDamage(...)
    -> ApplyDamageUseCase
      -> ICombatTargetPort
        -> CombatTargetAdapter
          -> CombatTarget domain state

DamageAppliedEvent
  -> CombatTargetView
```

## 레이어 메모

- **Domain**: 데미지 타입, 데미지 룰, 전투 대상 상태
- **Application**: `ApplyDamageUseCase`, `ICombatTargetPort`, `DamageAppliedEvent`
- **Infrastructure**: 포트 구현체와 타깃 상태 저장소
- **Presentation**: 데미지 반응/피격 피드백 뷰
- **Bootstrap**: 어댑터, UseCase, View 초기화와 공개 진입점 제공

## 현재 구현 기준 결정

- Combat은 우선 로컬 전투 대상으로 시작한다.
- 네트워크 전투 동기화는 이 피처 범위에 포함하지 않는다.
- 타깃 식별은 `EntityIdHolder`를 사용해 런타임 `DomainEntityId`를 공유한다.

## 피처 간 의존

- **Shared**: `EventBus`, `Result`, `EntityIdHolder`, `DomainEntityId`
