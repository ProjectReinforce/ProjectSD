# Combat Feature

대상 방어력 기반 데미지 계산과 데미지 적용 이벤트 발행을 담당한다.

## 현재 책임

- `DamageRule`로 최종 데미지를 계산한다.
- 타깃 포트를 통해 데미지를 적용한다.
- `DamageAppliedEvent`를 발행한다 (AttackerId 포함).
- `ICombatTargetProvider`를 통해 외부 피처(Player 등)가 데미지 파이프라인에 참여한다.

## 데이터 흐름

```text
ProjectileHitEvent
  -> CombatBootstrap.OnProjectileHit()
    -> CombatNetworkEventHandler.HandleProjectileHit()
      -> ApplyDamageUseCase.Execute(targetId, baseDamage, damageType, attackerId)
        -> ICombatTargetPort.GetDefense / ApplyDamage
          -> CombatTargetAdapter (내부 ICombatTargetProvider에 위임)
        -> DamageAppliedEvent 발행

DamageAppliedEvent
  -> CombatTargetView (HP 바 등)
  -> PlayerDamageEventHandler (Player Feature, PlayerHealthChangedEvent 발행)
```

## 레이어 메모

- **Domain**: `DamageType`, `DamageRule`, `CombatTarget`
- **Application**: `ApplyDamageUseCase`, `CombatNetworkEventHandler`, `ICombatTargetPort`, `ICombatTargetProvider`, `ICombatNetworkCommandPort`, `DamageAppliedEvent`
- **Infrastructure**: `CombatTargetAdapter` (ICombatTargetPort 구현, ICombatTargetProvider 기반 딕셔너리)
- **Presentation**: `CombatTargetView` (데미지 반응/피격 피드백)
- **Bootstrap**: `CombatBootstrap` (조립, 이벤트 구독, `RegisterTarget` API), `CombatTestTargetLoop` (테스트용)

## 현재 구현 기준 결정

- 타깃 식별은 `EntityIdHolder`를 사용해 런타임 `DomainEntityId`를 공유한다.
- `ICombatTargetProvider`: Combat이 소유하는 인터페이스. 외부 피처가 구현하면 데미지 파이프라인에 참여 가능.
  - `CombatBootstrap.RegisterTarget(id, provider)`로 등록
  - 기존 Inspector CombatTarget은 내부 `CombatTargetWrapper`로 래핑

## 피처 간 의존

- **Projectile**: `ProjectileHitEvent` (트리거)
- **Player**: `PlayerCombatTargetProvider`가 `ICombatTargetProvider` 구현
- **Shared**: `EventBus`, `Result`, `EntityIdHolder`, `DomainEntityId`
