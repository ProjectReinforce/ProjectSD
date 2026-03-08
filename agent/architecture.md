# /agent/architecture.md

## Architecture

The project follows a **Feature-first Clean Architecture**.

Structure:

Assets/Scripts_/Features/<FeatureName>/
Domain/
Application/
Presentation/
Infrastructure/
Bootstrap/

Assets/Scripts_/Shared/

Each feature must be self-contained.

Rules:

* Do not move feature-specific code into Shared.
* Shared must only contain reusable cross-feature utilities.
* Infrastructure implements Application ports.
* Domain must stay framework-independent.
* Bootstrap handles composition and wiring between layers.

Features should grow independently.
Only split features when a concept gains an independent lifecycle.
Never move feature-specific code into Shared.
