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

* Application may depend on Domain and Shared only.

* Presentation may depend on Application, Domain and Shared only.

* Infrastructure may depend on Application, Domain and Shared only.

Never reference:

* Unity API in Domain
* Photon API in Domain
* Database logic in Domain
