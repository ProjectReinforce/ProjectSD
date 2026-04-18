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

## 7. 현재 구조 (재구성 전)

```
Assets/Scripts/
├── Adapter/          ← 실제 Unity 구현
│   ├── Entity/{Player, Enemy, Boss, BossChaos}/
│   ├── Skill/        ← Skill, SkillExecutor, SkillSpawnerFactory + Effects/Projectile/Spread/Trajectories/TriggerEffects
│   ├── UI/, Manager/, Network/
├── Domain/           ← 인터페이스, ValueObjects, 수식
├── Data/             ← ScriptableObject DB
├── Application/, AppService/, InfraStructure/
├── Presentation/     ← DamagePopup 등
├── BootStrap/, Editor/, Testing/, WFC/
```

## 8. 재구성 로드맵

1. 현재 Adapter-중심 구조에서 Feature-first 로 전환.
2. 각 Feature 에 Domain/Application/Adapter/Presentation 4레이어 생성.
3. .asmdef 로 의존성 고정.
4. `architecture-guardian` 서브에이전트로 PR마다 레이어 위반 감사.

상세 Phase 진행 상태는 [implementation-roadmap.md](implementation-roadmap.md).

## 9. 작성 후 체크리스트

- [ ] CLAUDE.md "2. 아키텍처 원칙" 섹션과 동기화
- [ ] `architecture-guardian` 서브에이전트 규칙이 본 문서 반영
- [ ] Feature-first 재구성 진행 시 본 문서 업데이트
