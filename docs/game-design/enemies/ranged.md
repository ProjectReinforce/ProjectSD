# 적 설계서: 원거리형 (Ranged)

> **SSOT:** 이 문서의 수치는 `Assets/Data/Enemy/Ranged/*.asset` (SO 4종)의 복제본이다.
> 밸런싱 수정은 **SO에서 먼저** 하고 이 문서는 그 결과를 반영한다.
> 참조 SO: `Assets/Data/Enemy/Ranged/{RangedTurretShot, RangedTurretZone, RangedKiteShot, RangedKiteZone}.asset`
> 최종 동기화: 2026-04-24

## 1. 메타

| 항목 | 값 |
|---|---|
| 엔티티 ID (베이스) | `enemy_ranged` |
| 한국어 이름 | 원거리형 |
| 영어 이름 | Ranged |
| `enemyType` | `Ranged` (4) |
| 분류 | 기본 (원거리) |
| 등장 시점 | DifficultyData `rangedRatioStart/End` = **1 / 1** (시간 전구간 동일 비중) |
| 등장 비율 | 다른 기본 타입과의 비율 조합 체계 별도(코드 측 정책 확인 필요) |
| 최종 업데이트 | 2026-04-24 |

## 2. 컨셉

원거리에서 공격해 플레이어의 이동 동선을 제약하는 타입. 근접 적이 플레이어를 압박하는 동안 뒤에서 위협을 얹어 밀집·이동 타이밍을 강제한다.

## 3. 변형 (Variants) — 4종

모든 변형이 동일한 **공용 기본 스탯**을 가지며, `rangedBehavior` × `rangedAttack` 두 enum만 다르다.

### 3.1 행동 변형 (`rangedBehavior`)

| 값 | 이름 | 설명 |
|---|---|---|
| 0 | Turret (고정형) | 스폰된 자리에서 움직이지 못함. 플레이어가 사거리 안에 있으면 원거리 공격만 반복 |
| 1 | Kite (추격형) | 사거리 안으로 들어올 때까지 추적, 사거리 도달 시 정지 후 공격 |

### 3.2 공격 방식 (`rangedAttack`)

| 값 | 이름 | 설명 |
|---|---|---|
| 0 | Projectile (투사체) | 플레이어 방향으로 투사체 1발 발사 |
| 1 | Zone (경고 비주얼) | 대상 지점에 경고 표시 → 일정 시간 후 데미지 |

### 3.3 조합 → SO 파일

| 조합 | SO 파일 | 프리팹 |
|---|---|---|
| Turret + Projectile | `RangedTurretShot.asset` | Turret Shot |
| Turret + Zone | `RangedTurretZone.asset` | Turret Zone |
| Kite + Projectile | `RangedKiteShot.asset` | Kite Shot |
| Kite + Zone | `RangedKiteZone.asset` | Kite Zone |

## 4. 공용 스탯 (현재 SO 값, 4 변형 동일)

| 필드 | 값 |
|---|---|
| `baseHP` | **30** |
| `moveSpeed` | **0.48** (Kite 이동 시에만 사용, Turret은 사실상 0) |
| `contactDamage` | **10** |
| `expValue` | **5** |
| `attackRange` | **2** |
| `attackInterval` | **3** (초) |
| `attackDamage` | **20** |
| `projectileSpeed` | 1 |
| `projectileLifetime` | 2 |
| `telegraphDuration` | **1.3** (초) |
| `telegraphRadius` | **0.5** |

## 5. 이동 패턴

### 5.1 Turret (고정형)
- 구현: `StationaryMovement` (Features/Enemy/Adapter/StationaryMovement.cs)
- 스폰 즉시 현재 위치 정착. 플레이어가 사거리 안이면 공격 사이클 실행.

### 5.2 Kite (추격형)
- 구현: `KiteMovement(stopDistance = attackRange)` (Features/Enemy/Adapter/KiteMovement.cs)
- 거리 > 사거리: 추적 → 사거리 도달: 정지 후 공격 → 공격 후 재평가.

## 6. 공격 패턴

### 6.1 Projectile
```
state: Idle → Aiming → Firing → Cooldown(attackInterval)
```
`EnemyProjectile` + `SpawnManager.RaiseEnemyProjectile`.

### 6.2 Zone (Telegraph)
```
state: Idle → Warning(telegraphDuration) → Strike → Cooldown(attackInterval)
```
`TelegraphZone` (DOTween 금지, 자체 타이머 + GameState.Paused 체크).

## 7. 보상

- **경험치:** 5 (공용)
- **드랍:** `EnemyDropTable.asset` 공용 (엘리트가 아니므로 정수 드랍 없음).

## 8. 데이터 계약

- **SO 타입:** `EnemyData` (서브타입 없음, enum으로 variant 구분)
- **에셋 경로:** `Assets/Data/Enemy/Ranged/{variant}.asset`
- **주요 필드:** `rangedBehavior`, `rangedAttack`, `attackRange`, `attackInterval`, `projectileSpeed`, `projectileLifetime`, `telegraphDuration`, `telegraphRadius`.

## 9. 네트워크

- 스폰/AI 호스트. 투사체는 각 클라 로컬 렌더, 히트 판정은 호스트.
- 경고 비주얼: 위치·타이밍을 호스트가 결정, RPC 로 동기화. Strike 판정도 호스트.

## 10. 구현 체크리스트

- [x] `StationaryMovement` 구현
- [x] `KiteMovement(stopDistance = attackRange)` 구현
- [x] 투사체 공격 프리팹 + 풀링 (`EnemyProjectile`)
- [x] 경고 비주얼 `TelegraphZone`
- [x] `EnemyAttack` / `EnemyTargeter` / `EnemyAttackCooldown` 3 컴포넌트
- [x] 네트워크 RPC
- [x] 4가지 조합 SO 생성 (완료)
- [ ] `photon-sync-auditor`
- [ ] 플레이테스트 (회피 타이밍·밀집 상황)

## 11. 오픈 이슈

- 등장 비율 — 현재 `rangedRatio = 1/1` 전구간 고정. 다른 타입과의 실제 스폰 비중 정책 확인 필요
- 4 변형 간 수치 차별화 여부 — 현재는 모두 동일 스탯
- 투사체 속도 1은 매우 느림 → 회피 난이도 밸런스 확인
