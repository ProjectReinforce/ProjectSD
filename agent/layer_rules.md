# /agent/layer_rules.md

## Domain

Domain contains pure business logic.

Allowed:

* Entities
* ValueObjects
* Domain rules
* Domain state
* Domain methods

Not allowed:

* Unity API
* Photon API
* file IO
* database access
* UI logic

---

## Application

Application contains use cases, ports, and events.

Allowed:

* UseCase classes
* Repository interfaces
* Network port interfaces
* Domain event structs (e.g. LobbyUpdatedEvent)

Not allowed:

* Unity API (including UnityEngine.Debug.Log/LogWarning/LogError)
* Photon API
* MonoBehaviour
* Direct file IO

Rules:

* UseCases coordinate domain logic.
* UseCases must remain thin.
* Business rules must stay inside Domain.
* UseCases publish events via IEventBus — they do not call View or Presenter directly.

---

## Presentation

Handles user interaction and UI rendering.

Allowed:

* View (MonoBehaviour)
* InputHandler

Rules:

* Do not place business logic here.
* View subscribes to IEventBus and renders itself.
* InputHandler receives user input and calls UseCases directly.

---

## Infrastructure

Handles external systems.

Allowed:

* Photon adapters
* Persistence adapters
* External SDK integrations

Rules:

* Must implement Application ports.
* Must not contain business logic.

---

## Bootstrap

Handles composition root for a feature.

Allowed:

* Object creation
* Dependency wiring
* Initialization order
* Event subscription (delegate handling to Application)

Not allowed:

* Event handling logic (subscribe here, handle in Application EventHandler)
* Business logic (position calculation, damage formula, selection logic)
* Domain entity creation (use UseCase instead)

Rules:

* Must not contain business logic or rendering logic.
* All wiring for a feature must live in that feature's Bootstrap.
