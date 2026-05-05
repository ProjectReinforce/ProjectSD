# Architecture Overview

본 문서는 ProjectSD / Sweepin' Dreams 의 레이어 구조와 의존성 규칙을 정의한다. [CLAUDE.md § 2](../../CLAUDE.md) 와 **반드시 일치**해야 함 — 둘 중 하나를 고치면 다른 하나도 같이 고친다.

## 1. 목표 구조 (Feature-first + Clean Architecture)

각 Feature(Skill, Enemy, Character, UI 등)는 내부에 자체 레이어를 가진다.

```
Feature/
├── Domain/         ← 순수 C# 비즈니스 로직. UnityEngine/Photon 의존 금지
├── Application/    ← 유스케이스, Domain 조립. UnityEngine 금지 (원칙)
├── Adapter/        ← MonoBehaviour, Photon RPC, ScriptableObject 등 Unity 연결부
└── Presentation/   ← UI 바인딩, 이펙트, 애니메이션 트리거
```

## 2. 의존성 방향 (하드 룰)

```
Presentation ──▶ Adapter ──▶ Application ──▶ Domain
                                               ▲
                                    Data (SO) ─┘
```

- **상위 → 하위 단방향.** 역방향 참조 금지.
- **Domain 레이어 순수성:** `Domain/` 의 어떤 파일도 `using UnityEngine;`, `using Photon.*;` 를 포함하면 안 됨. 발견 시 `architecture-guardian` 서브에이전트가 경고.
- **Application 에 `UnityEngine` 원칙적 금지.** 정말 필요한 경우(코루틴 등)는 추상화 레이어 통해서.

## 3. Feature 경계

- 서로 다른 Feature 의 `Adapter/`, `Application/` 을 **직접 참조 금지.**
- 공유가 필요하면 `Domain/Shared/` 로 승격.
- 예: Skill 이 Enemy 의 체력을 깎으려면 Enemy Adapter 에서 노출된 공용 인터페이스(`IDamageable`)를 사용. Enemy Adapter 구체 타입을 import 하지 않는다.

## 4. 공유 코드 위치

| 위치 | 용도 |
|---|---|
| `Assets/Scripts/Shared/` (계획) | 여러 Feature 가 쓰는 공용 도메인 타입 |
| `Assets/Scripts/Domain/` | 기존 Domain 인터페이스·ValueObject |
| `Assets/Scripts/Data/` | ScriptableObject 타입 (모든 Feature 에서 read-only 로 참조 가능) |

## 5. 어셈블리 정의(.asmdef) 전략

- 현재 프로젝트는 .asmdef 을 **전면 도입 전**. Feature-first 재구성 시 각 Feature 에 .asmdef 부여 예정.
- 목표: Feature 간 컴파일 의존성이 폴더 구조로 강제되도록 — Adapter → Domain 방향만 허용.

## 6. 외부 의존성 경계

서드파티는 Adapter 레이어에서만 직접 사용. Domain/Application 은 추상화만 참조.

| 외부 | 경계 위치 | 비고 |
|---|---|---|
| Photon PUN 2 | `Assets/Scripts/Adapter/Network/`, 각 Feature Adapter | RPC/ObservedComponent 등 |
| DamageNumbersPro | `Assets/Scripts/Presentation/` | 래핑 권장 |
| DOTween Pro | `Assets/Scripts/Presentation/`, UI Adapter | |
| Unity AssetStore SFX/BGM | `Assets/Scripts/Adapter/Audio/` | |

## 7. 현재 구조 (Feature-first 재구성 완료)

```
Assets/Scripts/
├── Features/
│   ├── Skill/
│   │   ├── Domain/             ← Formulas (DamageFormula), ValueObjects (9종)
│   │   ├── Application/        ← DamageService
│   │   ├── Adapter/            ← Skill, SkillExecutor, Spawner 5종, Effects/Projectile/Spread/Trajectories/TriggerEffects, SkillManager, ChaosSkillManager
│   │   │   └── Data/           ← SkillData + SkillSubTypes (7종)
│   │   └── Presentation/       (현재 비어 있음, UI Feature 로 이동)
│   ├── Enemy/
│   │   └── Adapter/            ← Enemy, EnemyMovement, EnemyContact, EnemyAnimator, Movement/
│   │       └── Data/           ← EnemyData
│   ├── Boss/
│   │   ├── Domain/             ← BossPhase, Interfaces (IBossAttackPattern, IBossChaosEffect)
│   │   ├── Application/        ← BossPhaseService
│   │   ├── Adapter/            ← Boss, BossAnimator, Attack/, BossChaosEffects, BossSpawner, BossPhaseManager, BossChaosApplicator
│   │   │   └── Data/           ← BossData
│   │   └── Presentation/       ← BossHealthBarUI, BossWarningUI
│   ├── Character/              ← Player
│   │   ├── Domain/             ← ValueObjects (StatModifier, StatModifierCollection, StatType)
│   │   └── Adapter/            ← Player, PlayerMovement/Health/Stats/Visual/Animator/Spawner/Stub, Camerafollow, HitEffect, GamePlayerSpawner, RespawnManager
│   │       └── Data/           ← CharacterData, CharacterDatabase
│   ├── Progression/            ← 경험치 · 레벨업
│   │   ├── Domain/             ← Formulas (LevelTable)
│   │   ├── Application/        ← ExperienceService
│   │   └── Adapter/            ← ExperienceOrb, LevelUpManager
│   └── UI/
│       ├── Adapter/            ← Common/, Menu/ (RoomList, Lobby 포함)
│       └── Presentation/       ← UImanager, InGameHUD, LevelUpPanel, SkillCardUI, DamagePopup, DeathOverlayUI, ReconnectUI, ResultPanelUI, DebugOverlay
├── Shared/
│   ├── Domain/                 ← IDamageable, IPoolable, GameResult
│   ├── Data/                   ← AudioLibrary, DifficultyData, GameplayConfig
│   ├── Managers/               ← GameManager, NetworkManager, PoolManager, SpawnManager, AudioManager, DifficultyManager, HostMigrationHandler, ResultManager, GameStatTracker, SceneTransitionManager, GameAudioConnector
│   └── Network/                ← NetworkAdapter
├── BootStrap/, Editor/, Testing/, WFC/    ← 유지
```

**namespace 규약:** `SwDreams.Features.{Feature}.{Layer}[.{Sub}]` / `SwDreams.Shared.{Sub}`

## 8. 재구성 완료 상태 및 후속 작업

**완료 (2026-04-18):**
- ✅ 141개 `.cs` 파일을 Features/Shared 로 재배치
- ✅ namespace 일괄 재작성 + using / fully-qualified 경로 교체
- ✅ 빈 최상위 폴더 (Adapter/, Data/, Domain/, Application/, Presentation/ 등) 제거

**후속 작업 (별도 PR, 우선순위 순 — `architecture-guardian` 감사 결과 반영):**

1. **Shared → Features 역방향 의존 해소 (Critical, 29건)** — `Shared/Managers/` 의 `GameManager`, `NetworkManager`, `SpawnManager`, `ResultManager`, `HostMigrationHandler`, `GameAudioConnector`, `DifficultyManager`, `GameStatTracker` 등이 Features 다수를 직접 import. `Shared/Data/GameplayConfig`, `Shared/Network/NetworkAdapter` 도 동일. 구조적 원인: 이들은 게임 전체를 오케스트레이션하는 "god 객체" 성격이라 Shared 가 아니라 **`Assets/Scripts/Composition/` (또는 `Bootstrap/`) 레이어**로 분리해야 원칙 준수 가능.

2. **Domain 레이어 순수성 위반 (Critical, 4건)**
   - `Features/Boss/Domain/Interfaces/IBossAttackPattern.cs` — `using UnityEngine;` (Vector3/Transform 노출 추정)
   - `Features/Skill/Domain/ValueObjects/SkillTriggerEffect.cs` — `using UnityEngine;`
   - `Features/Skill/Domain/ValueObjects/TriggerContext.cs` — `using UnityEngine;` (TriggerEffect 파이프라인 전체 기반이라 가장 시급)
   - `Features/Character/Domain/ValueObjects/StatModifierCollection.cs` — 같은 Feature 의 Adapter import (주석과 실제 모순, 레이어 역방향)

3. **Feature 간 Adapter ↔ Adapter 직접 참조 (대량)** — Skill 의 `AreaZone/OrbitalObject/PlacedTurret/Projectile` 이 `Boss/Character.Adapter` 직접 참조, `ChaosSkillManager/SkillManager` 가 `Character/Progression` 참조, `Boss.Adapter` 가 `Character/Skill` 참조 등. `Shared/Domain/Interfaces/IDamageable` 로 다운캐스트 경로 통일 시 약 70% 해소 예상.

4. **알려진 경계 위반 (세부)**
   - `Skill → PlayerStats/PlayerHealth` 직접 참조 → `IPlayerStateProvider` 공유 인터페이스로 격리
   - `BossHealthBarUI → Boss` 직접 참조 → 이벤트 구독 방식

5. **Application 레이어의 `UnityEngine` import** — `Features/Boss/Application/BossPhaseService.cs` 가 `using UnityEngine;` (Mathf/Debug 추정). `System.Math` 로 치환 또는 제거.

6. **`.asmdef` 도입** — 각 Feature 에 어셈블리 부여하여 컴파일 타임에 역방향 의존 차단. 위 1~5 가 선결되어야 에러 없이 부여 가능.

7. **namespace-class 이름 충돌 완화** — `Features.Boss` (namespace) vs `Boss` (class) 같은 충돌이 fully-qualified 참조를 강제하는 곳 5건 이상 존재. 장기적으로 `BossEntity`, `SkillEntity` 같은 이름으로 변경 고려.

상세 Phase 진행 상태는 [implementation-roadmap.md](implementation-roadmap.md).

## 9. 작성 후 체크리스트

- [ ] CLAUDE.md §3 "폴더 지도" 현재 구조로 갱신
- [ ] CLAUDE.md "2. 아키텍처 원칙" 섹션과 동기화 확인
- [ ] `architecture-guardian` 서브에이전트로 재구성 직후 레이어 위반 감사 1회 실행
- [ ] `.asmdef` 도입 PR 준비
