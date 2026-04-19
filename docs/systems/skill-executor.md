# Skill Executor — 발사 모드 & Stat 필터 & FireRecord

Sweepin' Dreams 의 모든 스킬은 `SkillExecutor`가 발사를 총괄한다. 본 문서는 **Executor 패턴의 4가지 발사 모드**, **applicableStats 필터**, **FireRecord (메아리용)** 의 시스템 계약을 정의한다.

> 개별 스킬의 적용 예시는 [game-design/skills/INDEX.md](../game-design/skills/INDEX.md) 에서 시작해 각 스킬 문서 참조. TriggerEffect 핸들러는 [trigger-effects.md](trigger-effects.md).

## 1. 전체 구조

```
Skill (쿨다운 타이머)
  └─ SkillExecutor.Execute(firingMode, context)
       ├─ firingMode 분기 (아래 4종)
       │    ├─ SimultaneousSpread → ISkillSpawner 즉시 호출 n회
       │    ├─ DelayedBurst → Update 타이머로 count회 지연 발사
       │    ├─ TwoPhase → Phase1 실행 → 완료 콜백 → Phase2 실행
       │    └─ Single → ISkillSpawner 1회 호출
       ├─ applicableStats 필터로 PlayerStats 주입
       ├─ IFireRecorder 에 발사 기록 남김 (OnFire 트리거)
       └─ 완료 시 풀에 반환
```

### 핵심 컴포넌트

| 컴포넌트 | 파일 | 역할 |
|---|---|---|
| `Skill` | `Assets/Scripts/Features/Skill/Adapter/Skill.cs` | 쿨다운 관리, Executor 호출 |
| `SkillExecutor` | `Assets/Scripts/Features/Skill/Adapter/SkillExecutor.cs` | 발사 모드 분기, Spawner 선택, applicableStats 필터 적용 |
| `ISkillSpawner` | `Assets/Scripts/Features/Skill/Adapter/ISkillSpawner.cs` | 실제 오브젝트 생성 인터페이스 |
| `SkillSpawnerFactory` | `Assets/Scripts/Features/Skill/Adapter/SkillSpawnerFactory.cs` | SkillData 타입별 Spawner 선택 |
| `IFireRecorder` + `FireRecord` | `Assets/Scripts/Features/Skill/Adapter/IFireRecorder.cs` | 메아리/Refire용 발사 기록 (인터페이스·VO 정의 완료, 호출부·구현체 미작성) |
| `SpreadPatterns` | `Assets/Scripts/Features/Skill/Adapter/Spread/SpreadPatterns.cs` | 부채꼴·360° 균등분할 방향 계산 |

Spawner 구현체: `ProjectileSpawner`, `AreaSpawner`, `OrbitalSpawner`, `DebuffSpawner`, `PlacedSpawner` (`Features/Skill/Adapter/`).

## 2. 발사 모드 (4종)

### 2.1 SimultaneousSpread (동시 다방향)

- 한 프레임에 `count`개 오브젝트를 **동시 발사**.
- 방향은 `SpreadPatterns` 로 계산: 부채꼴 or 360°/n 균등분할.
- **적용 스킬:** 표창, 부메랑, 회오리바람.

### 2.2 DelayedBurst (딜레이 연발)

- `count`만큼 **시간차로 발사/생성**. Executor가 `Update` 에서 타이머로 처리.
- 각 발사 시점 기준으로 방향/위치 재계산 가능 (예: 매직 미사일은 매 발사 시 "가장 가까운 적" 재조준).
- **적용 스킬:** 매직 미사일, 번개, 개미지옥, 자동포탑.

### 2.3 TwoPhase (2페이즈)

- Phase 1 실행 → **완료 콜백** → Phase 2 실행.
- Phase 1/Phase 2 각각 독립적인 발사 모드 사용 가능.
- 완료 판정은 Executor 내부 플래그로 관리.
- **적용 스킬:** 장검 (회전 → 발사) — *최근 커밋 `1f225a555` 에서 Phase 2 발사 동작 복구*.

### 2.4 Single (단일)

- 오브젝트 **1개만 생성**. 투사체 개수 스탯을 **무시**.
- **적용 스킬:** 성역, 저주인형.

## 3. applicableStats 필터

각 스킬은 SO 필드 `applicableStats`로 **이 스킬이 반영할 플레이어 스탯만** 선언한다.

- 예: 성역은 "체력 회복량 증가" 만 받고 "투사체 속도"는 무시.
- Executor가 발사 시점에 필터를 통해 해당 스킬에 적용되는 스탯만 `PlayerStats` 에서 읽어 주입.
- **각 Effect가 직접 `playerStats.XXX` 를 읽는 하드코딩은 금지.**

**현재 상태 (2026-04-19):**
- ✅ `PlayerStats.GetFilteredXxx(SkillData)` 메서드 8종 정의됨 (`PlayerStats.cs:361-418`): `GetFilteredStat`, `GetFilteredProjectileCount`, `GetFilteredProjectileSpeed`, `GetFilteredAttackMultiplier`, `GetFilteredSkillRangeBonus`, `GetFilteredSkillDurationBonus`, `GetFilteredKnockbackMultiplier`, `GetFilteredHealMultiplier`.
- ✅ `SkillExecutor.BuildContext()` 가 발사 시점에 위 메서드들을 호출하여 컨텍스트에 주입 완료 (`SkillExecutor.cs:276, 286, 299, 305, 311, 317`).
- ✅ Projectile/Area 등 Effect 측은 `context.attackMultiplier` 등 사전 계산된 값을 읽으므로 하드코딩 제거됨.
- ⚠️ 신규 추가될 스탯이 있으면 위 두 곳(필터 메서드 + Executor 호출)에 짝을 맞춰 추가해야 한다.

## 4. IFireRecorder (메아리용)

- Executor가 발사 시 `{ skillId, origin, direction, timestamp }` 를 기록.
- **메아리(#17)** 스킬이 이 기록을 읽어 **데미지 계수만 낮춰서 재실행**.
- **진화형 메아리**는 최근 2개 기록을 재현.
- [trigger-effects.md § Refire](trigger-effects.md) 핸들러가 이 인터페이스를 호출하도록 설계됨.

**현재 상태:**
- ✅ `IFireRecorder` 인터페이스 + `FireRecord` struct 정의 완료 (`Features/Skill/Adapter/IFireRecorder.cs`).
- ⬜ 호출부(`SkillExecutor` 내 기록), 구현체(메모리 버퍼), `RefireHandler` 미작성.
- **우선순위:** 메아리 스킬 구현 시점에 함께 작성.

## 5. 체인 비행 (Projectile 시스템)

ChainHandler(TriggerEffect)와는 **다른** 개념. 투사체가 적중 시 소멸하지 않고 **다음 적으로 물리적으로 날아가는** 메커니즘.

| SkillData 필드 | 용도 | 예시 |
|---|---|---|
| `trajectoryType` | `Homing` 필수 | Homing |
| `chainFlightCount` | 체인 횟수 (0이면 비활성) | 3 |
| `chainSearchRadius` | 다음 타겟 탐색 반경 | 5.0 |

**동작:** 적중 → 소멸 대신 이미 맞은 적 제외 → 주변 탐색 → `HomingTrajectory` 타겟 교체 → 계속 비행 → 체인 소진 시 소멸.

**적용:** 진화형 매직 미사일(체인 미사일)에 사용. [skills/magic-missile.md](../game-design/skills/magic-missile.md) 참조.

## 6. 구현 시 주의사항

### 6.1 Executor 수명주기
- 오브젝트 풀링 사용. 생성/소멸 오버헤드 방지.
- 딜레이 연발 중 **플레이어 사망/레벨업/씬 전환** 시 Executor 즉시 정리.
- 2페이즈에서 Phase 1 완료 전 소멸 시 Phase 2 스킵.

### 6.2 네트워크
- **Executor는 호스트에서만 실행.** 결과물(투사체/장판)은 각 클라이언트가 로컬 렌더.
- 히트 판정은 호스트. 상세는 [network-sync.md](network-sync.md).

### 6.3 정리 대상 / 완료 사항

**완료:**
- ✅ `HomingProjectile`, `BoomerangProjectile`, `TornadoProjectile`, `SpiralTornadoProjectile`, `ExplodingProjectile`, `ChainProjectile` 등 서브클래스 삭제 완료. 동작은 `Trajectory` + `TriggerEffect` 조합으로 대체.
- ✅ `applicableStats` 하드코딩 제거 — Executor 필터 경유로 전환 완료 (§ 3 참조).
- ✅ `SkillData.subProjectilePrefab` SO 필드화 완료 — `ProjectileSpawner` → `Projectile.SetSubProjectilePrefab` → `TriggerContext.subProjectilePrefab` 으로 전달 (`SpawnProjectileHandler.cs:35` 에서 읽음).

**잔여:**
- 디버그 로그 정리: `Projectile`, `ExplodeHandler`, `ChainHandler` 등에 남아있는 임시 로그 제거.
- `IFireRecorder` 호출부·구현체·`RefireHandler` 작성 (메아리 스킬 구현 시).

## 7. SO 설정 원칙

- **모든 수치는 SO에서 설정.** 데미지, 쿨타임, 범위, 처형 기준, 체인 횟수, 딜레이 등. 하드코딩 금지.
- `applicableStats` 필터는 SO 인스펙터에서 개별 설정.
- 진화 조건(액티브 + 패시브 조합)도 SO.
- 발사 모드는 `SkillData` 서브클래스의 enum으로 구분.

## 8. 기존 코드 참조

- `Assets/Scripts/Features/Skill/Adapter/` — Executor, Spawner 5종, Spread, TriggerEffects, Trajectories, IFireRecorder, Skill, SkillSpawnerFactory
- `Assets/Scripts/Features/Skill/Adapter/Data/SkillData.cs` (+ 서브타입: `ProjectileSkillData`, `AreaSkillData`, `OrbitalSkillData`, `PlacedSkillData`, `DebuffSkillData`, `PassiveSkillData`, `ChaosSkillData`)
- `Assets/Scripts/Features/Skill/Adapter/Spread/SpreadPatterns.cs`
- `Assets/Scripts/Features/Skill/Adapter/Trajectories/` — `ITrajectoryBehavior`, `HomingTrajectory`, `BoomerangTrajectory`, `PullTrajectories`, `SimpleTrajectories`(Tornado/Spiral/Zigzag/SinWave 포함), `TrajectoryFactory`
- `Assets/Scripts/Features/Skill/Domain/ValueObjects/` — `FiringMode`, `TriggerType`, `EffectActionType`, `TrajectoryType` (7종 enum), `SpreadPatternType`, `AimType`, `SkillTriggerEffect`, `TriggerContext`, `DamageResult`
- `Assets/Scripts/Features/Character/Adapter/PlayerStats.cs` — `GetFilteredXxx(SkillData)` 8종 메서드

## 9. 알려진 제약 / 남은 작업

- [ ] `IFireRecorder` **호출부·구현체·`RefireHandler` 미작성** — 메아리(#17) 스킬 구현 시 함께 작성
- [ ] 디버그 로그 정리 (Projectile/ExplodeHandler/ChainHandler 등 임시 로그)
