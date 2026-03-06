# AGENT.md

This project uses rule-based guidance for code generation agents.

Follow the rules defined in the `/agent` directory.

Rule priority (highest first):

1. dependency_rules.md
2. layer_rules.md
3. architecture.md
4. feature_rules.md
5. naming_rules.md
6. anti_patterns.md

When generating or modifying code:

* Always follow dependency direction rules.
* Prefer adding code inside the current feature.
* Avoid introducing new shared abstractions.
* Keep domain logic inside Domain layer.
* Keep use cases thin.

If rules conflict, follow the priority order above.
