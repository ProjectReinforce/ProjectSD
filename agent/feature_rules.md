# /agent/feature_rules.md

## Feature Isolation

Each feature owns:

* Domain
* Application
* Presentation
* Infrastructure
* Bootstrap

Example:

Assets/Scripts_/Features/
Lobby/
Domain/
Application/
Presentation/
Infrastructure/
Bootstrap/

Rules:

* Features must not depend on other feature internals.
* Cross-feature communication must happen through ports or events.
* Wiring/composition across a feature's layers must live in that feature's Bootstrap.

Keep concepts inside a feature unless they have an independent lifecycle.

Example:

Room belongs to Lobby if its lifecycle depends on Lobby.

If Room gains independent lifecycle, it may become its own feature later.

---

## EventBus 전환 기준

현재 각 피처 Bootstrap이 로컬 `new EventBus()`를 생성한다.

전환 트리거:

* **피처 간 이벤트가 처음 필요해지는 시점**
* EventBus 구현 교체/격리 테스트 요구가 실제로 생기는 시점

전환 시 적용할 구조:

* SceneContext (MonoBehaviour) 도입 — 씬 레벨에서 단일 EventBus 소유
* IEventPublisher / IEventSubscriber 인터페이스 분리 (ISP)
  * UseCase는 IEventPublisher만 의존 (Publish)
  * View는 IEventSubscriber만 의존 (Subscribe/Unsubscribe)
  * EventBus는 두 인터페이스 모두 구현
* 각 피처 Bootstrap은 SceneContext로부터 EventBus를 주입받음
* 전환 전까지는 피처 로컬 EventBus 사용을 허용
