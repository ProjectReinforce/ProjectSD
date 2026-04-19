# ProjectSD — Claude 작업 가이드

이 문서는 Claude가 **모든 세션 시작 시 자동으로 읽는** 프로젝트 안내서다. 긴 설계 문서는 여기 직접 쓰지 말고 `docs/` 하위에 두고 링크만 걸 것.

> **버전:** v2 (2026-04-18) — `docs/` 재정리 완료 후 갱신.

---

## 1. 프로젝트 개요

- **이름:** ProjectSD / **Sweepin' Dreams** (게임 타이틀 — "Sweet Dream + Sweep")
- **엔진:** Unity 2D URP + Photon PUN 2 멀티플레이 (보이스챗은 Photon Voice 2 예정, 미구현)
- **장르/컨셉:** **1~4인 Co-op Survivors-like** (Vampire Survivors 계열). 보스전 제외 최대 15분.
- **핵심 시스템:** 스킬 시스템(진화·혼돈 포함), 적/보스 AI, 플레이어 스탯/패시브, 정수·무기, 프레임 UI(팝업/토스트).
- **출시 순서:** **Stove Indie → Steam.** 한국 게임 등급 분류를 Stove 인디로 먼저 받고 Steam 출시. 상세 [docs/systems/platform-integration.md](docs/systems/platform-integration.md).
- **현재 상태:** Feature-first 재구성 준비 중. 브랜치 `Skill_Refactor` — 스킬 시스템 2차 리팩터링 이후 잔여 작업. 상세는 [docs/architecture/implementation-roadmap.md](docs/architecture/implementation-roadmap.md).

---

## 2. 아키텍처 원칙

**목표 구조: Feature-first + Clean Architecture**

각 Feature(Skill, Enemy, Character, UI 등)는 내부에 자체 레이어를 가진다.

```
Feature/
├── Domain/         ← 순수 C# 비즈니스 로직. UnityEngine, Photon 의존 금지
├── Application/    ← 유스케이스, Domain을 조립. UnityEngine 금지 (원칙)
├── Adapter/        ← MonoBehaviour, Photon RPC, ScriptableObject 등 Unity 연결부
└── Presentation/   ← UI 바인딩, 이펙트, 애니메이션 트리거
```

**의존성 방향 (하드 룰):**
```
Presentation ──▶ Adapter ──▶ Application ──▶ Domain
                                               ▲
                                    Data (SO) ─┘
```

**금지 사항:**
- Domain 레이어의 파일에 `using UnityEngine;`, `using Photon.*;` 등 외부 프레임워크 import 금지
- 하위 레이어가 상위 레이어를 참조하는 역방향 의존 금지
- 다른 Feature의 내부(Adapter/Application) 직접 참조 금지. 공유가 필요하면 `Domain/Shared`로 승격

---

## 3. 폴더 지도 (Feature-first 전환 완료 후 — 커밋 `78853035f`)

```
Assets/Scripts/
├── Features/                ← 각 Feature 내부에 자체 레이어
│   ├── Skill/
│   │   ├── Domain/          ← FiringMode, TriggerType, EffectActionType, TrajectoryType (7종), SkillTriggerEffect, TriggerContext, DamageFormula
│   │   ├── Adapter/         ← SkillExecutor, SkillSpawnerFactory, ISkillSpawner + {Projectile|Area|Orbital|Placed|Debuff}Spawner, Trajectories/, TriggerEffects/Handlers/, Spread/, IFireRecorder
│   │   └── Adapter/Data/    ← SkillData (+ ProjectileSkillData/AreaSkillData 등 서브타입)
│   ├── Enemy/               ← Adapter (Enemy, EnemyContact + Movement/)
│   ├── Boss/                ← Domain(Formulas) + Application(BossPhaseService) + Adapter(Boss, BossChaosApplicator, BossPhaseManager, BossSpawner)
│   ├── Character/           ← Adapter (Player/PlayerStub, PlayerStats, PlayerMovement, PlayerHealth, PlayerVisual, RespawnManager) + Adapter/Data (CharacterData)
│   ├── Progression/         ← Adapter (Levelupmanager, SkillManager, ChaosSkillManager, 진화·경험치)
│   └── UI/
│       ├── Adapter/Menu/    ← MenuSceneManager, TitlePanelController, RoomListPanelController, WaitingRoomPanelController, CharacterSelectUI, RoomList/, Common/
│       └── Presentation/    ← UImanager, InGameHUD, LevelUpPanel, SkillCardUI, ResultPanelUI, DamagePopup, DeathOverlayUI, ReconnectUI, DebugOverlay, UIBackgroundBlur, UIImageBlur
├── Shared/                  ← Feature 경계를 넘는 공유 코드
│   ├── Domain/              ← 순수 C# (GameResult, PlayerBuildData, IDamageable, IPoolable)
│   ├── Data/                ← AudioLibrary, DifficultyData, GameplayConfig
│   ├── Managers/            ← GameManager, NetworkManager, ResultManager, SpawnManager, AudioManager, GameAudioConnector, PoolManager, GameStatTracker, DifficultyManager, HostMigrationHandler, TestManager
│   └── Network/             ← NetworkAdapter
├── Editor/                  ← 에디터 전용 (SkillDataEditor 등)
├── Testing/                 ← 테스트 엔트리 (Phase2TestEntry 등)
└── WFC/                     ← 맵 Wave Function Collapse
```

UI 프리팹: `Assets/Resources/Prefabs/UI/FrameToast.prefab`, `LevelUpPanel.prefab`, `Frame_InputPassWord.prefab`, `Frame_MakeRoom.prefab`, `Frame_RoomList.prefab`, `Frame_SearchRoom.prefab`, `InGameHUD.prefab`, `BossHealthBar.prefab`, `ChaosIcon.prefab`, `DeathOverlay.prefab`, `ReconnectOverlay.prefab`, `Panel_Lobby.prefab`, `Panel_Title.prefab`, `RoomListPanel.prefab`, `Slot_RoomList.prefab`, `SkillSlot.prefab`, `TeammateEntry.prefab`. (`Frame_PopUp.prefab` 은 아직 미작성 — [docs/systems/ui-frame.md](docs/systems/ui-frame.md))

**신규 파일 위치 원칙:** Feature 내부는 `Domain/Application/Adapter/Presentation` 레이어 구조 유지. Feature 경계를 넘는 것만 `Shared/` 로. 상세는 [docs/architecture/overview.md](docs/architecture/overview.md).

---

## 4. 도메인 용어집

**게임 전반**
- **Sweepin' Dreams:** 게임 타이틀. 1~4인 Co-op Survivors-like. 상세 [docs/game-design/overview.md](docs/game-design/overview.md).
- **Run:** 한 판(세션). 보스전 제외 최대 15분.
- **6슬롯 제한:** 액티브 + 패시브 합계 최대 6. [docs/game-design/rules.md](docs/game-design/rules.md).
- **4등급 체계:** 일반 / 희귀 / 영웅 / 전설 — 혼돈 스킬·능력치 공용.

**스킬 시스템**
- **Skill (스킬):** 플레이어 자동 발동 능력. 액티브 24종 / 패시브 19종 / 혼돈 19종. `Features/Skill/Adapter/`.
- **Executor:** 쿨다운 도달 시 발사를 담당하는 컴포넌트. 4가지 발사 모드. [docs/systems/skill-executor.md](docs/systems/skill-executor.md).
- **발사 모드:** `SimultaneousSpread` / `DelayedBurst` / `TwoPhase` / `Single`.
- **Trajectory:** 발사체 궤적. `Straight / Homing / Boomerang / Tornado / Spiral / Zigzag / SinWave` (7종). `Features/Skill/Adapter/Trajectories/`.
- **TriggerEffect:** 이벤트(OnFire/OnHit/OnKill/OnExpire/OnInterval/OnPlayerHit) × 액션(DealDamage/Explode/Chain/ApplyDoT/... 11종 enum, 핸들러 10종 등록 + Refire 미구현) 매핑. [docs/systems/trigger-effects.md](docs/systems/trigger-effects.md).
- **Evolution (진화):** 액티브 + 특정 패시브가 모두 최대 레벨 시 발동. 10종 조합(예: 장검+스킬 범위 → 검무). 2슬롯→1슬롯.
- **Chaos Skill (혼돈 스킬):** 게임 규칙 자체를 바꾸는 스킬. **레벨 10/20/30에 1회씩 선택.** 레벨 30 미선택 1개가 보스에게 부여.
- **applicableStats 필터:** 스킬이 어떤 플레이어 스탯을 반영하는지 SO로 선언. Executor 경유 주입 경로 완성 (`PlayerStats.GetFilteredXxx` → `SkillExecutor.BuildContext`).
- **IFireRecorder:** 메아리(#17) 스킬용 발사 기록 인터페이스. 인터페이스·`FireRecord` VO 정의 완료, 호출부와 구현체는 메아리 구현 시 작성.

**적 / 보스**
- **일반 적 4종:** 기본 추적형 / 빠른형 / 둔한형 / 무리형. [docs/game-design/enemies/INDEX.md](docs/game-design/enemies/INDEX.md).
- **원거리형 (Ranged):** 2행동(고정형/추격형) × 2공격(투사체/경고 비주얼) = 4 변형.
- **엘리트형 (Elite):** 무리형 제외 타입의 강화 버전. 정수 드랍 소스.
- **Boss / BossChaos:** Boss는 3페이즈 + 미선택 혼돈 스킬 1개. `BossChaos` 폴더는 혼돈 적용 로직 담당. [docs/game-design/enemies/boss.md](docs/game-design/enemies/boss.md).

**UI / 네트워크**
- **Frame:** UI 팝업/토스트 프레임워크. `Frame_PopUp` (모달·일시정지 가능), `FrameToast` (비모달·짧은 알림). [docs/systems/ui-frame.md](docs/systems/ui-frame.md).
- **MenuScene / GameScene:** 2개 씬 구조. [docs/systems/scene-structure.md](docs/systems/scene-structure.md).
- **호스트-클라이언트:** Photon MasterClient 가 권위. 투사체는 로컬 렌더, 히트는 호스트. [docs/systems/network-sync.md](docs/systems/network-sync.md).
- **런타임 Effect source prefix:** `essence_*` / `weapon_*` / `chaos_*` / `buff_*`.
- **Voice (보이스챗):** Photon Voice 2 사용 예정. 무료 티어 20 CCU. 미구현 — 설계만 [docs/systems/voice-chat.md](docs/systems/voice-chat.md).

**플랫폼 / 인프라**
- **Platform Service:** Stove/Steam SDK 추상화 (`IPlatformService`). Phase A 추상화 → Phase B Stove → Phase C Steam. 상세 [docs/systems/platform-integration.md](docs/systems/platform-integration.md).

**정수 / 무기**
- **Essence (정수):** 엘리트 드랍, 속성 부여(얼음/불/번개). 최대 2개. 조합 히든 효과.
- **Weapon (무기):** 낮은 확률 드랍, LoL 아이템식 스탯 부여. 조합 시스템.

---

## 5. 참조 문서

설계 문서는 `docs/` 하위에 `.md`로 둔다. Claude는 **필요한 순간에만** 읽는다.

### 진입점
- [docs/README.md](docs/README.md) — **폴더 지도·SSOT 규칙 먼저 확인**
- [docs/game-design/overview.md](docs/game-design/overview.md) — GDD 전반 (Sweepin' Dreams)
- [docs/architecture/implementation-roadmap.md](docs/architecture/implementation-roadmap.md) — Phase별 구현 진행도 (현재 Phase 5 진행 중)

### 폴더별
- [docs/architecture/](docs/architecture/) — 레이어·의존성, 구현 로드맵
- [docs/game-design/](docs/game-design/) — overview, flow-design, rules, skills/ (24종), enemies/ (7종)
- [docs/systems/](docs/systems/) — skill-executor, trigger-effects, network-sync, ui-frame, managers, scene-structure, spawn-rules, damage-formula, **voice-chat**, **platform-integration**
- [docs/templates/](docs/templates/) — skill/enemy/system-spec 양식

### 작업 유형별 참조 우선순위
- **새 스킬 추가 →** `docs/templates/skill-spec.md` 복사 → `docs/game-design/skills/{skill-id}.md` 작성 → 구현 시 [skill-executor.md](docs/systems/skill-executor.md), [trigger-effects.md](docs/systems/trigger-effects.md)
- **적/보스 추가 →** `docs/templates/enemy-spec.md` → `docs/game-design/enemies/{enemy-id}.md`
- **새 시스템 설계 →** `docs/templates/system-spec.md` → `docs/systems/{system-id}.md`
- **아키텍처/레이어 관련 →** `docs/architecture/overview.md`
- **네트워크 변경 →** `docs/systems/network-sync.md` + `photon-sync-auditor` 서브에이전트
- **보이스챗(마이크) 구현 →** [docs/systems/voice-chat.md](docs/systems/voice-chat.md) 만 보면 완결. Photon Voice 2 기반
- **Steam/Stove SDK 통합 →** [docs/systems/platform-integration.md](docs/systems/platform-integration.md). Phase A(추상화) → B(Stove) → C(Steam) 순

### SSOT 규칙
같은 정보는 한 곳에만 둔다. 상세는 [docs/README.md § SSOT 규칙](docs/README.md).

---

## 6. 작업 규칙

1. **도메인 레이어 순수성:** `Assets/Scripts/Domain/**` 또는 Feature 내 `Domain/` 파일에 `UnityEngine`, `Photon` import 금지. 발견 시 중단하고 사용자에게 보고.
2. **설계 먼저, 코드 나중:** 새 스킬/적/시스템 추가 요청이 오면, 먼저 해당 템플릿의 `.md` 설계서 작성 제안. 사용자가 이미 설계가 있다고 답하면 건너뜀.
3. **네트워크 동기화 변경:** `[PunRPC]`, `RaiseEvent`, `PhotonView`, `IPunObservable`을 건드렸다면 PR/커밋 전에 `photon-sync-auditor` 서브에이전트 호출 제안.
4. **MonoBehaviour 생명주기:** 초기 참조 캐싱은 `Awake`, 다른 컴포넌트 의존 초기화는 `Start`. `Update` 안에서 `GetComponent`/`Find` 금지.
5. **ScriptableObject 생성:** 신규 SO는 `Data/` 하위에 두고, `CreateAssetMenu` 경로를 일관된 루트(`ProjectSD/Data/...`)로 유지.
6. **Assets 외부는 수정 금지:** `Library/`, `Temp/`, `obj/`, `Logs/`, `UserSettings/`는 Unity가 생성/관리. 절대 Write 대상이 아님.

---

## 7. 빌드 & 테스트

> (사용자가 채워주세요)
>
> - 빌드 스크립트: ?
> - 테스트 실행: Unity Test Runner 사용 중인지 ?
> - Play Mode 자동 테스트: ?

---

## 8. 서브에이전트 호출 타이밍

`.claude/agents/` 에 정의됨. Claude가 스스로 판단하여 호출하거나, 사용자가 "유니티 관점에서 리뷰해줘" 같은 문구로 명시 호출 가능.

| 에이전트 | 언제 쓰나 |
|---|---|
| `unity-reviewer` | C# 코드 수정 직후 일반 품질 리뷰 |
| `skill-architect` | 새 스킬/진화 설계·구현 시 |
| `architecture-guardian` | 레이어/폴더 간 의존성 변경, 재구성 작업 시 |
| `photon-sync-auditor` | 네트워크 RPC/동기화 코드 변경 시 |

---

## 9. 사용자 프로필

- Claude와 처음 장기 협업하는 개발자. 시스템 전체를 **이해하며** 쓰고 싶어함.
- 신규 개념(서브에이전트, 훅, 메모리 등) 도입 시 "무엇/왜/어떻게"를 한 줄씩 함께 설명할 것.
- 대화 언어: **한국어 기본.** 코드/파일명은 영어 유지.
