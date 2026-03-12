# AGENTS.md

This project uses rule-based guidance for code generation agents.

Follow the rules defined in the `/agent` directory.

Rule priority (highest first):

1. dependency_rules.md
2. layer_rules.md
3. architecture.md
4. feature_rules.md
5. naming_rules.md
6. anti_patterns.md

Current project structure (source of truth):

* `Assets/Scripts_/Features/<FeatureName>/Domain`
* `Assets/Scripts_/Features/<FeatureName>/Application`
* `Assets/Scripts_/Features/<FeatureName>/Presentation`
* `Assets/Scripts_/Features/<FeatureName>/Infrastructure`
* `Assets/Scripts_/Features/<FeatureName>/Bootstrap`
* `Assets/Scripts_/Shared`

Current active feature-first implementation:

* `Assets/Scripts_/Features/Lobby/*`

Legacy area:

* `Assets/Scripts/*` exists, but new feature-first code should be added under `Assets/Scripts_/*` unless the task explicitly targets legacy files.

Design principles:

* Colocate code that changes for the same reason — if a single requirement change forces edits across multiple classes or files, those pieces belong together. Conversely, if one class changes for multiple unrelated reasons, split it.
* Minimize the ripple effect of changes — a class exposes only what it does (interface), never how it does it (implementation). If changing an implementation forces callers to change too, the boundary is wrong.


When generating or modifying code:

* Always follow dependency direction rules.
* When investigating files for a feature, first read `Assets/Scripts_/Features/<FeatureName>/README.md` if it exists.
* Prefer adding code inside the current feature.
* Avoid introducing new shared abstractions.
* Keep domain logic inside Domain layer.
* Keep use cases thin.
* Keep composition/wiring in Bootstrap.
* If a new feature is introduced, create the same 5-layer folder layout under `Assets/Scripts_/Features/<NewFeature>/`.

If rules conflict, follow the priority order above.

## Agent Reasoning

* Do not jump to yes/no — first check project rules and current code, then answer based on evidence.
* Do not start with the conclusion and retrofit the reasoning.
* If a user request conflicts with or shows signs of violating Design Principles, flag it before proceeding.
