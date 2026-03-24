# /agent/anti_patterns.md

## Anti Patterns

Never do these:

* Put business logic inside View or InputHandler.
* Put networking logic inside Domain.
* Put Unity API usage inside Domain.
* Put feature-specific code inside Shared.
* Let Bootstrap become a god class.
* Make one port responsible for unrelated behaviors.
* Introduce architectural layers not defined in architecture.md.

* Silent failure on null — returning silently without logging when a required reference is null. Use `Debug.LogError` for missing SerializeField/injected dependencies; do not add null checks for internal data parameters (let NullReferenceException surface naturally).
* Behavioral switch on type enums — use Factory + Strategy pattern instead. Switch is acceptable for command dispatch and simple value mapping.
* Strategy pattern file structure — enum, interface, factory는 한 파일에 둔다. 구현체(Strategy 클래스)는 각각 별도 파일.
* GetComponent로 의존성 획득 — `[SerializeField]`로 Inspector에서 명시적으로 연결한다. 어떤 의존성이 필요한지 코드와 Inspector 모두에서 보여야 한다.

* 이중 상태 — 같은 개념(체력, 위치 등)을 두 도메인 엔티티가 각각 관리하면 안 된다. 하나의 진실 원천(Single Source of Truth)만 존재해야 한다. 예: Player.CurrentHp와 CombatTarget.CurrentHealth가 동시에 존재하면 반드시 어긋난다.
* 이중 경로 데미지 — 한 이벤트(투사체 히트 등)에 대해 데미지를 두 번 적용하지 않는다. 하나의 UseCase가 데미지를 계산하고, 결과 이벤트를 통해 다른 피처가 반응하는 구조여야 한다.
* 소비자가 아닌 제공자에 포트 배치 — 피처 A가 피처 B의 기능을 호출할 때, 포트 인터페이스는 호출하는 쪽(A)의 Application에 정의한다. 구현은 호출당하는 쪽(B)의 Infrastructure에 둔다. (Dependency Inversion Principle)
* Bootstrap에 선택/순환 로직 — Bootstrap은 조립만 한다. 스킬 순환 선택, 다음 대상 결정 등의 로직은 Application 레이어의 별도 클래스로 분리한다.
* Application 이벤트에 Unity 타입 — Application 레이어의 이벤트에 Sprite, GameObject 등 Unity 타입을 넣지 않는다. Presentation 레이어에서 포트를 통해 resolve한다.

When unsure:

Prefer keeping code inside the current feature rather than Shared.
