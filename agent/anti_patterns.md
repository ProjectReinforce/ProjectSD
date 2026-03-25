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
* Field null checks outside Initialize — null checks for field members (`[SerializeField]`, injected dependencies) must ONLY appear in `Initialize()` / `Awake()` / `Start()`. Once initialization passes, assume fields are valid. Do not re-check them in event handlers, Update, or other runtime methods. If a field is null at runtime, the bug is in initialization — let NullReferenceException surface it.
* Behavioral switch on type enums — use Factory + Strategy pattern instead. Switch is acceptable for command dispatch and simple value mapping.
* Strategy pattern file structure — enum, interface, factory go in one file. Each implementation (Strategy class) gets its own file.
* GetComponent for dependency acquisition — use `[SerializeField]` and wire explicitly in Inspector. Dependencies must be visible in both code and Inspector.

* Dual state — the same concept (health, position, etc.) must not be managed by two domain entities independently. Only one Source of Truth may exist. Example: Player.CurrentHp and CombatTarget.CurrentHealth existing simultaneously will inevitably diverge.
* Dual-path damage — do not apply damage twice for a single event (e.g. projectile hit). One UseCase calculates damage; other features react via result events.
* Port on provider instead of consumer — when feature A calls feature B, define the port interface in A's Application. Implementation goes in B's Infrastructure. (Dependency Inversion Principle)
* Bootstrap containing selection/cycling logic — Bootstrap only wires. Skill rotation, next-target selection, etc. must be extracted to a dedicated Application-layer class.
* Unity types in Application — do not put Sprite, GameObject, AudioClip, Color, Debug.Log/LogWarning/LogError, or any UnityEngine API in Application-layer events, ports, or use cases. If a port needs Unity types, it belongs in Presentation, not Application/Ports. Logging belongs in Bootstrap/Infrastructure.

---

## Established Patterns (Lessons Learned)

These patterns were discovered through refactoring and should be followed:

### 1. Port Placement by Type Dependency
**Rule:** Port interfaces go in Application/Ports ONLY if they use no Unity types. Ports that reference UnityEngine types (Sprite, GameObject, AudioClip, Color, etc.) belong in Presentation.
- ✅ `Application/Ports/IZoneEffectPort.cs` — uses only `Float3` (Shared), OK in Application
- ✅ `Presentation/ISkillEffectPort.cs` — uses `GameObject`, `AudioClip` (Unity), must be in Presentation
- ✅ `Presentation/ISkillIconPort.cs` — uses `Sprite` (Unity), must be in Presentation
- ❌ `Application/Ports/ISkillEffectPort.cs` — Unity types in Application layer violates layer rules

**Why:** Application layer must not depend on UnityEngine. Moving a Unity-typed port to Application "infects" every Application class that references it.

### 2. Event Handlers → Application Layer
**Rule:** Event handling logic should be in Application layer, not Bootstrap.
- ❌ `CombatBootstrap.OnProjectileHit()` with business logic — WRONG
- ✅ Dedicated EventHandler class in Application layer — CORRECT

**Why:** Bootstrap should only wire components together. Event handling contains business logic that belongs in Application.

**Pattern:**
```
Bootstrap (wiring only)
  → Creates Application EventHandler
  → Subscribes to events
  → EventHandler contains the handling logic
```

### 3. Bootstrap Responsibilities
**Bootstrap SHOULD:**
- Instantiate classes
- Inject dependencies (constructor injection)
- Subscribe to external events
- Call Initialize() on components

**Bootstrap SHOULD NOT:**
- Execute business logic
- Create domain entities directly
- Contain selection/decision logic
- Handle game events

### 4. Infrastructure with MonoBehaviour
**Rule:** Infrastructure CAN have MonoBehaviour when needed for Unity integration.
- ✅ `CombatTargetAdapter : MonoBehaviour` — needs SerializeField for scene integration
- ✅ `PlayerNetworkAdapter : MonoBehaviourPun` — required for Photon

Pure C# adapters are preferred when possible.

### 5. Domain Entity Creation via UseCase
**Rule:** Bootstrap must not create domain entities directly.
```
❌ Bootstrap creates Domain.Projectile directly
✅ Bootstrap calls SpawnProjectileUseCase → UseCase creates Domain.Projectile
```

---

When unsure:

Prefer keeping code inside the current feature rather than Shared.
