# /agent/anti_patterns.md

## Anti Patterns

Never do these:

* Put business logic inside Presenter.
* Put networking logic inside Domain.
* Put Unity API usage inside Domain.
* Put feature-specific code inside Shared.
* Let EntryPoint become a god class.
* Make one port responsible for unrelated behaviors.
* Introduce architectural layers not defined in architecture.md.

* Silent failure on null — returning silently without logging when a required reference is null. Use `Debug.LogError` for missing SerializeField/injected dependencies; do not add null checks for internal data parameters (let NullReferenceException surface naturally).
* Behavioral switch on type enums — use Factory + Strategy pattern instead. Switch is acceptable for command dispatch and simple value mapping.

When unsure:

Prefer keeping code inside the current feature rather than Shared.
