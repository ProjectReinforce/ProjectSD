# /agent/feature_rules.md

## Feature Isolation

Each feature owns:

* Domain
* Application
* Presentation
* Infrastructure

Example:

Features/
Lobby/
Domain/
Application/
Presentation/
Infrastructure/

Rules:

* Features must not depend on other feature internals.
* Cross-feature communication must happen through ports or events.

Keep concepts inside a feature unless they have an independent lifecycle.

Example:

Room belongs to Lobby if its lifecycle depends on Lobby.

If Room gains independent lifecycle, it may become its own feature later.
