# 스킬 설계서: 표창 (Shuriken)

## 1. 메타

| 항목 | 값 |
|---|---|
| 스킬 ID | 1 (`skillId`) |
| 한국어 이름 | 표창 |
| 영어 이름 | Shuriken |
| 카테고리 | 액티브 |
| 유형 | 투사체 (ProjectileSkillData) |
| 진화 여부 | Yes (폭렬 표창) |
| 최종 업데이트 | 2026-04-24 |

> **SSOT:** 이 문서의 수치는 `Assets/Data/Skill/Active/001_Shuriken.asset` 의 복제본이다.

## 2. 컨셉

가장 단순하고 기본적인 투사체. 부채꼴로 퍼져 적들을 훑는 느낌. Survivors-like 의 "쓸어버리는 재미"의 핵심 도구.

## 3. 기본 동작

| 항목 | 값 |
|---|---|
| 조준 | 가장 가까운 적 방향 기준 부채꼴 |
| 발사 모드 | SimultaneousSpread |
| 궤적(Trajectory) | Straight (SimpleTrajectory) |
| 관통 | 적중 시 소멸 |
| 투사체 개수 스탯 적용 | 발사 수(부채꼴 방향 수) 증가 |

**동작 설명:** 부채꼴 모양으로 투사체 동시 발사. 각 투사체는 가장 가까운 적 방향을 중심으로 부채꼴로 퍼져 날아감.

## 4. 수치 (현재 SO 값)

### 4.1 레벨별

| 레벨 | 데미지 (`damagePerLevel`) | 쿨다운 (`cooldownPerLevel`) |
|---|---|---|
| 1 | **12** | **1.80s** |
| 2 | **15** | 1.65s |
| 3 | **18** | 1.50s |
| 4 | **22** | 1.38s |
| 5 | **26** | 1.28s |
| 6 | **31** | 1.17s |
| 7 | **38** | **1.08s** |

### 4.2 발사 파라미터

| 필드 | 값 |
|---|---|
| `projectileSpeed` | 5 |
| `projectileCount` | 1 (투사체 수 스탯으로 확장) |
| `projectileLifetime` | 3초 |
| `penetrates` | false (적중 시 소멸) |
| `trajectoryType` | Straight (0) |
| `aimType` | 가장 가까운 적 방향 (0) |
| `spreadPattern` | Spread (1, 부채꼴) |
| `spreadAngle` | **30°** |
| `knockbackForce` | 2 |
| `maxInstances` | 3 |

## 5. TriggerEffect 매핑

| Trigger | EffectAction | 파라미터 | 용도 |
|---|---|---|---|
| OnHit | DealDamage | (base, 0, 0) | 기본 적중 데미지 |

*진화형은 아래 섹션 참조.*

## 6. 진화 경로

- **진화 조건:** 표창 + 투사체 속도 증가 (둘 다 최대 레벨)
- **진화 후 이름:** 폭렬 표창 (Explosive Shuriken)
- **주요 변화:** 적중 시 폭발 (범위 데미지 추가). 투사체는 여전히 적중 시 소멸.
- **TriggerEffect 추가:**

| Trigger | EffectAction | 파라미터 | 용도 |
|---|---|---|---|
| OnHit | DealDamage | (base, 0, 0) | 기본 적중 데미지 |
| OnHit | Explode | (radius, 1.0, 0) | 폭발 (context.damage 100%) |

## 7. 데이터 계약 (ScriptableObject)

- **SO 타입:** `ProjectileSkillData`
- **에셋 경로:** `Assets/Data/Skill/Active/001_Shuriken.asset`
- **evolutionPair GUID** (진화 파트너 패시브): `ba7a877839522d64bafacee54b21442f`
- **evolvedSkill GUID:** `50a6600abd685eb43a0b18812eeea29e` (201_EvolvedShuriken)
- **주요 필드:** trajectoryType=Straight, aimType=Closest, spreadPattern=Spread, applicableStats=[투사체 속도, 투사체 개수, 공격력, 치명타 확률/데미지]

## 8. 네트워크 동기화

기본 규약은 [systems/network-sync.md](../../systems/network-sync.md).

- Executor는 호스트에서 실행.
- 투사체는 각 클라이언트 로컬 렌더 (동기화 안 함).
- 히트 판정은 호스트.

## 9. 구현 체크리스트

- [x] SO 생성 (001_Shuriken.asset)
- [x] SpreadPatterns 부채꼴 적용 확인
- [ ] 진화형(201_EvolvedShuriken) ExplodeHandler 연결
- [ ] `photon-sync-auditor`
- [ ] 플레이테스트

## 10. 오픈 이슈

- 레벨별 수치 TBD (밸런싱)
- 부채꼴 각도가 투사체 개수에 따라 어떻게 변하는지 정책 확정 필요 (고정 각도 vs 균등 분할)
