# /agent/dependency_rules.md

## Dependency Direction

Allowed dependency flow:

Presentation -> Application -> Domain

Infrastructure -> Application

Shared -> no feature dependency

Cross-feature dependency is encouraged. Any layer may depend on another feature as long as the layer direction is respected. Do not add abstractions to Shared just to avoid cross-feature imports.

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

## Cross-Feature Port Placement

When feature A uses functionality from feature B:

1. **Port interface** is defined in the **consumer (A)**'s `Application/Ports/`.
2. **Implementation** lives in the **provider (B)**'s `Infrastructure/`.
3. **Bootstrap** creates the implementation and injects it into the consumer.

```
Combat/Application/Ports/ICombatTargetProvider.cs   ← Combat defines (consumer)
Player/Infrastructure/PlayerCombatTargetProvider.cs  ← Player implements (provider)
```

This way the consumer owns the interface, so the provider's internal changes do not affect the consumer.

**Forbidden**: defining the port in the provider's Application and having the consumer import it. This couples the consumer to the provider's internal contract.
