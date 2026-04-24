# 스킬 설계서: 자동포탑 (Auto Turret)

## 1. 메타

| 항목 | 값 |
|---|---|
| 스킬 ID | 8 (`skillId`) |
| 한국어 이름 | 자동포탑 |
| 영어 이름 | Auto Turret |
| 카테고리 | 액티브 |
| 유형 | 소환 (설치) |
| 진화 여부 | Yes (미니건 포탑) |
| 최종 업데이트 | 2026-04-24 |

> **SSOT:** 이 문서의 수치는 `Assets/Data/Skill/Active/008_AutoTurret.asset` 의 복제본이다.

## 2. 컨셉

설치형 소환수. 사거리 내 가장 가까운 적을 항상 치명타로 공격. 투사체 개수 스탯을 올릴수록 포탑 수가 늘어 "영지화"가 가능.

## 3. 기본 동작

| 항목 | 값 |
|---|---|
| 조준 | 포탑이 사거리 내 가장 가까운 적 |
| 발사 모드 | DelayedBurst (포탑 순차 설치) |
| 궤적 | — (포탑 자체 + 포탑 투사체) |
| 관통 | — (포탑 고정) |
| 투사체 개수 스탯 적용 | 포탑 생성 수 증가 |

**동작:** 투사체 개수 스탯만큼 약간의 간격으로 포탑 생성. 각 포탑은 사거리 내 가장 가까운 적을 공격. **항상 치명타.**

## 4. 수치 (현재 SO 값)

### 4.1 레벨별

| 레벨 | 포탑 공격력 (`damagePerLevel`) | 설치 쿨다운 |
|---|---|---|
| 1 | **8** | **8.0s** |
| 2 | 10 | 7.5s |
| 3 | 12 | 7.0s |
| 4 | 15 | 6.5s |
| 5 | 18 | 6.0s |
| 6 | 22 | 5.5s |
| 7 | **27** | **5.0s** |

### 4.2 발사 파라미터

| 필드 | 값 |
|---|---|
| `firingMode` | DelayedBurst (1) |
| `burstDelay` | 1.0초 (포탑 순차 설치 간격) |
| `attackRange` | **1.5** (포탑 사거리) |
| `attackCooldown` | **0.5초** (포탑 발사 간격) |
| `maxInstances` | 2 (동시 존재 포탑 수) |

## 5. TriggerEffect 매핑

| Trigger | EffectAction | 파라미터 | 용도 |
|---|---|---|---|
| OnHit | DealDamage | (base, 0, 0) | 포탑 투사체 적중 (항상 치명타) |

## 6. 진화 경로

- **진화 조건:** 자동포탑 + 치명타 데미지 증가
- **진화 후 이름:** 미니건 포탑 (Minigun Turret)
- **주요 변화:** **쿨타임 증가 + 지속시간 감소, 대신 공격 속도 대폭 상승**. 항상 치명타 유지.
- 순수 SO 수치 변경만으로 구현 가능 (TriggerEffect 추가 없음).

## 7. 데이터 계약

- **SO 타입:** `ProjectileSkillData` (설치형 소환 모드)
- **에셋 경로:** `Assets/Data/Skill/Active/008_AutoTurret.asset`
- **evolvedSkill:** `208_EvolvedAutoTurret.asset`
- **주요 필드:** firingMode=DelayedBurst, attackRange=1.5, attackCooldown=0.5, applicableStats=[투사체 개수, 공격력, 치명타 데미지]
- 관련 컴포넌트: `Assets/Scripts/Features/Skill/Adapter/Spawners/PlacedSpawner.cs`

## 8. 네트워크

- 포탑 생성은 호스트. 각 클라이언트가 로컬 렌더링.
- 포탑의 타겟 선정·발사는 호스트 판정.

## 9. 구현 체크리스트

- [ ] SO 생성
- [ ] PlacedTurret 컴포넌트 연결
- [ ] "항상 치명타" 설정 확인
- [ ] 진화형 수치 변환 (공속↑, 지속↓)
- [ ] `photon-sync-auditor`
- [ ] 플레이테스트

## 10. 오픈 이슈

- 동시 포탑 상한 (성능)
- 포탑 위치 결정 (플레이어 주변 랜덤 vs 지정 등)
