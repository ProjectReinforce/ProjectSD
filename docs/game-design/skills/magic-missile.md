# 스킬 설계서: 매직 미사일 (Magic Missile)

## 1. 메타

| 항목 | 값 |
|---|---|
| 스킬 ID | 2 (`skillId`) |
| 한국어 이름 | 매직 미사일 |
| 영어 이름 | Magic Missile |
| 카테고리 | 액티브 |
| 유형 | 투사체 (유도) |
| 진화 여부 | Yes (체인 미사일) |
| 최종 업데이트 | 2026-04-24 |

> **SSOT:** 이 문서의 수치는 `Assets/Data/Skill/Active/002_MagicMissile.asset` 의 복제본이다.

## 2. 컨셉

정면으로 발사 후 가장 가까운 적을 추적. 연발되며 정교하게 적을 지우는 느낌. 투사체 개수 스탯을 쌓을수록 연발 수가 늘어난다.

## 3. 기본 동작

| 항목 | 값 |
|---|---|
| 조준 | 플레이어 정면 방향 + 발사 후 가장 가까운 적 유도 |
| 발사 모드 | DelayedBurst |
| 궤적 | Homing |
| 관통 | 적중 시 소멸 |
| 투사체 개수 스탯 적용 | 연발 수 증가 |

**동작:** 플레이어 정면으로 발사되며, 발사 후 가장 가까운 적을 추적. 투사체 개수 스탯만큼 약간의 딜레이를 두고 연속 발사.

## 4. 수치 (현재 SO 값)

### 4.1 레벨별

| 레벨 | 데미지 (`damagePerLevel`) | 쿨다운 (`cooldownPerLevel`) |
|---|---|---|
| 1 | **10** | **2.10s** |
| 2 | 13 | 1.95s |
| 3 | 16 | 1.80s |
| 4 | 20 | 1.65s |
| 5 | 24 | 1.50s |
| 6 | 29 | 1.38s |
| 7 | **35** | **1.28s** |

### 4.2 발사 파라미터

| 필드 | 값 |
|---|---|
| `firingMode` | DelayedBurst (1) |
| `burstDelay` | **0.2초** |
| `projectileSpeed` | 3.5 |
| `projectileCount` | 1 |
| `projectileLifetime` | 4초 |
| `trajectoryType` | Homing (1) |
| `homingRotateSpeed` | 200 |
| `aimType` | 가장 가까운 적 (1) |
| `maxInstances` | 3 |

## 5. TriggerEffect 매핑

| Trigger | EffectAction | 파라미터 | 용도 |
|---|---|---|---|
| OnHit | DealDamage | (base, 0, 0) | 기본 적중 |

## 6. 진화 경로

- **진화 조건:** 매직 미사일 + 투사체 개수 증가
- **진화 후 이름:** 체인 미사일 (Chain Missile)
- **주요 변화:** 적중 시 소멸하지 않고 **타겟 교체 후 계속 비행**. 체인 카운트 0이 되면 소멸.

**진화형 SO 필드 (체인 비행 시스템 — TriggerEffect 아님):**

| 필드 | 값 |
|---|---|
| `trajectoryType` | `Homing` |
| `chainFlightCount` | (SO 설정) |
| `chainSearchRadius` | (SO 설정) |

[systems/skill-executor.md § 5 체인 비행](../../systems/skill-executor.md) 참조.

## 7. 데이터 계약

- **SO 타입:** `ProjectileSkillData`
- **에셋 경로:** `Assets/Data/Skill/Active/002_MagicMissile.asset`
- **evolvedSkill:** `202_EvolvedMagicMissile.asset`
- **주요 필드:** firingMode=DelayedBurst, trajectoryType=Homing, applicableStats=[투사체 속도, 투사체 개수, 공격력]

## 8. 네트워크

- Executor는 호스트. 타겟 선정도 호스트.
- 체인 타겟 교체는 호스트가 결정 후 RPC 반영.

## 9. 구현 체크리스트

- [ ] SO 생성
- [ ] HomingTrajectory 연결
- [ ] 진화형 체인 비행 파라미터 SO 설정 (`chainFlightCount`, `chainSearchRadius`)
- [ ] `photon-sync-auditor`
- [ ] 플레이테스트

## 10. 오픈 이슈

- 연발 간격 vs 발사 수의 밸런스 (너무 빨라지면 화면 낭비)
- 체인 카운트 SO 튜닝
