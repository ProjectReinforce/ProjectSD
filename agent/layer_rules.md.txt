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

Application contains use cases and ports.

Allowed:

* UseCase classes
* Repository interfaces
* Network port interfaces
* Output port interfaces

Rules:

* UseCases coordinate domain logic.
* UseCases must remain thin.
* Business rules must stay inside Domain.

---

## Presentation

Handles user interaction and UI flow.

Allowed:

* EntryPoint
* Presenter
* View
* InputHandler

Rules:

* Do not place business logic here.
* Presenter updates views.
* InputHandler receives user input.

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
