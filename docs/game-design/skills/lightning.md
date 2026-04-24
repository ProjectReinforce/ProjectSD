# 스킬 설계서: 번개 (Lightning)

## 1. 메타

| 항목 | 값 |
|---|---|
| 스킬 ID | 4 (`skillId`) |
| 한국어 이름 | 번개 |
| 영어 이름 | Lightning (파일명: `004_Lightening` — 오타) |
| 카테고리 | 액티브 |
| 유형 | 범위 (랜덤 위치) |
| 진화 여부 | Yes (뇌전역) |
| 최종 업데이트 | 2026-04-24 |

> **SSOT:** 이 문서의 수치는 `Assets/Data/Skill/Active/004_Lightening.asset` 의 복제본이다.

## 2. 컨셉

예측 불가능한 랜덤 낙뢰. 플레이어 주변 어디든 떨어질 수 있어 캐주얼한 "행운"의 느낌을 준다. 낙뢰 수 증가로 화려함이 커진다.

## 3. 기본 동작

| 항목 | 값 |
|---|---|
| 조준 | 주변 랜덤 위치 |
| 발사 모드 | DelayedBurst |
| 궤적 | 즉발 (Area) |
| 관통 | — (장판형) |
| 투사체 개수 스탯 적용 | 낙뢰 수 증가 |

**동작:** 주변 랜덤 위치에 낙뢰. 투사체 개수 스탯만큼 약간의 딜레이를 두고 연속 낙하. 매번 다른 랜덤 위치.

## 4. 수치 (현재 SO 값)

### 4.1 레벨별

| 레벨 | 데미지 | 쿨다운 |
|---|---|---|
| 1 | **25** | **3.75s** |
| 2 | 30 | 3.45s |
| 3 | 36 | 3.15s |
| 4 | 44 | 2.85s |
| 5 | 52 | 2.63s |
| 6 | 62 | 2.40s |
| 7 | **75** | **2.18s** |

### 4.2 발사 파라미터

| 필드 | 값 |
|---|---|
| `firingMode` | DelayedBurst (1) |
| `burstDelay` | 0.15초 |
| `areaRadius` | **0.4** |
| `areaDuration` | 0.3초 |
| `tickRate` | 0.3 |
| `spawnAtRandomPosition` | **true** |
| `randomSpawnRadius` | **3** (플레이어 주변 랜덤 반경) |
| `maxInstances` | 3 |

## 5. TriggerEffect 매핑

| Trigger | EffectAction | 파라미터 | 용도 |
|---|---|---|---|
| OnHit | DealDamage | (base, radius, 0) | 범위 데미지 |

## 6. 진화 경로

- **진화 조건:** 번개 + 스킬 유지 시간 증가
- **진화 후 이름:** 뇌전역 (Thunder Field)
- **주요 변화:** 낙뢰 지점에 **감전 지대(슬로우 + DoT 장판)** 생성.

**진화형 TriggerEffect:**

| Trigger | EffectAction | 파라미터 | 용도 |
|---|---|---|---|
| OnHit | DealDamage | (base, radius, 0) | 즉시 범위 데미지 |
| OnHit | ApplySlow | (slow%, duration, 0) | 장판 내 슬로우 |
| OnHit | ApplyDoT | (tick, duration, interval) | 장판 내 지속 피해 |

> `ApplyDoT` 는 적 개체 부착이므로 적이 장판을 벗어나도 지속. AreaZone 틱과 구분 — [systems/trigger-effects.md § 4](../../systems/trigger-effects.md).

## 7. 데이터 계약

- **SO 타입:** `ProjectileSkillData` (랜덤 낙하 장판)
- **에셋 경로:** `Assets/Data/Skill/Active/004_Lightening.asset`
- **evolvedSkill:** `204_EvolvedLightening.asset`
- **주요 필드:** firingMode=DelayedBurst, spawnAtRandomPosition=true, randomSpawnRadius=3, applicableStats=[스킬 범위, 공격력, 스킬 유지 시간]

## 8. 네트워크

- 낙뢰 위치 결정은 호스트. RPC로 각 클라이언트에 전파.
- 장판 판정/체류 시간은 호스트가 관리.

## 9. 구현 체크리스트

- [ ] SO 생성
- [ ] 진화형 AreaZone + DoT 조합 확인
- [ ] `photon-sync-auditor`
- [ ] 플레이테스트

## 10. 오픈 이슈

- 랜덤 위치 결정이 호스트 전용이면 클라마다 위치가 동일한지 확인
- 진화형 장판 겹침 정책 (중복 슬로우/DoT 갱신 or 누적)
