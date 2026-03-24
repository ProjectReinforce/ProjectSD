# /agent/dependency_rules.md

## Dependency Direction

Allowed dependency flow:

Presentation -> Application -> Domain

Infrastructure -> Application

Shared -> no feature dependency

Cross-feature dependency is encouraged. 모든 레이어에서 레이어 방향만 지키면 피처 간 적극적으로 의존한다. 피처 간 의존을 피하기 위해 Shared에 추상화를 추가하지 않는다.

Each layer may depend on:

* Domain: other features' Domain.
* Application: Domain, Shared, other features' Application or Domain.
* Presentation: Application, Domain, Shared, other features' same-or-inner layers.
* Infrastructure: Application, Domain, Shared, other features' same-or-inner layers.

Never reference:

* Unity API in Domain
* Photon API in Domain
* Database logic in Domain

---

## Cross-Feature Port 배치 원칙

피처 A가 피처 B의 기능을 사용할 때:

1. **포트 인터페이스**는 **호출하는 쪽(A)**의 `Application/Ports/`에 정의한다.
2. **구현체**는 **호출당하는 쪽(B)**의 `Infrastructure/`에 둔다.
3. **Bootstrap**에서 구현체를 생성하고 호출자에 주입한다.

```
Combat/Application/Ports/ICombatTargetProvider.cs   ← Combat이 정의 (소비자)
Player/Infrastructure/PlayerCombatTargetProvider.cs  ← Player가 구현 (제공자)
```

이렇게 하면 소비자가 인터페이스를 소유하므로, 제공자의 내부 변경이 소비자에 영향을 주지 않는다.

**금지**: 제공자의 Application에 포트를 정의하고 소비자가 그것을 import하는 구조. 이는 소비자를 제공자의 내부 계약에 종속시킨다.
