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
| `Skill` | `Assets/Scripts/Adapter/Skill/Skill.cs` | 쿨다운 관리, Executor 호출 |
| `SkillExecutor` | `Assets/Scripts/Adapter/Skill/SkillExecutor.cs` | 발사 모드 분기, Spawner 선택 |
| `ISkillSpawner` | `Assets/Scripts/Adapter/Skill/ISkillSpawner.cs` | 실제 오브젝트 생성 인터페이스 |
| `SkillSpawnerFactory` | `Assets/Scripts/Adapter/Skill/SkillSpawnerFactory.cs` | SkillData 타입별 Spawner 선택 |
| `IFireRecorder` | `Assets/Scripts/Adapter/Skill/IFireRecorder.cs` | 메아리/Refire용 발사 기록 (현재 stub) |
| `SpreadPatterns` | `Assets/Scripts/Adapter/Skill/Spread/SpreadPatterns.cs` | 부채꼴·360° 균등분할 방향 계산 |

Spawner 구현체: `ProjectileSpawner`, `AreaSpawner`, `OrbitalSpawner`, `DebuffSpawner`, `PlacedSpawner`.

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

**현재 상태 (2026-04-18):**
- `GetFilteredXxx()` 메서드는 이미 존재하나 **호출부가 비어있는 stub** 상태.
- 실제 스탯 주입 경로가 Executor를 거치지 않고 각 Effect가 읽는 구간이 남아있다.
- 스킬 리팩터링 2차(`c96861eff`~`deb23b669`)에서 일부 정리, 잔여분은 추후 작업.

## 4. IFireRecorder (메아리용)

- Executor가 발사 시 `{ skillId, origin, direction, timestamp }` 를 기록.
- **메아리(#17)** 스킬이 이 기록을 읽어 **데미지 계수만 낮춰서 재실행**.
- **진화형 메아리**는 최근 2개 기록을 재현.
- [trigger-effects.md § Refire](trigger-effects.md) 핸들러가 이 인터페이스를 호출하도록 설계되어 있으나 **현재 구현체 없음 (stub)**.
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

### 6.3 정리 대상 (기존 코드)
스킬 리팩터링 2차 이후 남은 정리 사항:

- **삭제 대상 서브클래스:** `HomingProjectile`, `BoomerangProjectile`, `TornadoProjectile`, `SpiralTornadoProjectile`, `ExplodingProjectile`, `ChainProjectile` — 외부 참조 0개 확인 완료. 동작은 `Trajectory` + `TriggerEffect` 조합으로 대체됨.
- **applicableStats 하드코딩 제거:** 각 Effect에서 `playerStats.XXX` 직접 읽는 코드 → Executor 필터로 전환.
- **디버그 로그 정리:** `Projectile`, `ExplodeHandler`, `ChainHandler` 에 임시 삽입된 로그 제거.

## 7. SO 설정 원칙

- **모든 수치는 SO에서 설정.** 데미지, 쿨타임, 범위, 처형 기준, 체인 횟수, 딜레이 등. 하드코딩 금지.
- `applicableStats` 필터는 SO 인스펙터에서 개별 설정.
- 진화 조건(액티브 + 패시브 조합)도 SO.
- 발사 모드는 `SkillData` 서브클래스의 enum으로 구분.

## 8. 기존 코드 참조

- `Assets/Scripts/Adapter/Skill/` — Executor, Spawner, Trajectories, TriggerEffects
- `Assets/Scripts/Data/SkillData.cs`, `Assets/Scripts/Data/SkillSubTypes/*.cs`
- `Assets/Scripts/Adapter/Skill/Spread/SpreadPatterns.cs`
- `Assets/Scripts/Adapter/Skill/Trajectories/` — `ITrajectoryBehavior`, `HomingTrajectory`, `BoomerangTrajectory`, `PullTrajectories`, `SimpleTrajectories`, `TrajectoryFactory`

## 9. 알려진 제약

- [ ] `IFireRecorder` **미구현 (stub)** — 메아리 구현 시 함께 작성
- [ ] `applicableStats` 필터 호출부 **일부 stub** — 2차 리팩터링 후속 작업
- [ ] `SpawnProjectileHandler.SetProjectilePrefab()` 은 현재 **코드 수동 설정** — `SkillData.subProjectilePrefab` 필드 추가 예정
