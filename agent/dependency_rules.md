# /agent/dependency_rules.md

## Dependency Direction

Allowed dependency flow:

Presentation -> Application -> Domain

Infrastructure -> Application

Shared -> no feature dependency

Rules:

* Domain must not depend on Application.

* Domain must not depend on Presentation.

* Domain must not depend on Infrastructure.

* Application may depend on Domain, Shared, and other features' Application or Domain.

* Presentation may depend on Application, Domain, Shared, and other features' same-or-inner layers.

* Infrastructure may depend on Application, Domain, Shared, and other features' same-or-inner layers.

Never reference:

* Unity API in Domain
* Photon API in Domain
* Database logic in Domain
