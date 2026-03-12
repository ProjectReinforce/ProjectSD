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
