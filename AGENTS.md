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

* 질문을 받았을 때 "맞다/아니다"부터 정하지 말고, 프로젝트 규칙과 현재 코드를 먼저 확인한 뒤 근거 위에서 답한다.
* 결론부터 말하고 근거를 끼워 맞추지 않는다.
