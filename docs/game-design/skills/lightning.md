# 스킬 설계서: 번개 (Lightning)

## 1. 메타

| 항목 | 값 |
|---|---|
| 스킬 ID | `skill_lightning_01` |
| 한국어 이름 | 번개 |
| 영어 이름 | Lightning |
| 카테고리 | 액티브 |
| 유형 | 범위 (랜덤 위치) |
| 진화 여부 | Yes (뇌전역) |
| 최종 업데이트 | 2026-04-18 |

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

## 4. 수치

*실제 값은 `Assets/Data/Skills/lightning_01.asset`. 하드코딩 금지.*

| 레벨 | 데미지 | 쿨다운 | 낙뢰 반경 | 낙뢰 수 | 기타 |
|---|---|---|---|---|---|
| 1 | — | — | — | — | *TBD* |

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

- **SO 타입:** `AreaSkillData` (진화형은 `SpawnArea` 후 지속)
- **에셋 경로:** `Assets/Data/Skills/lightning_01.asset`
- **주요 필드:** firingMode=DelayedBurst, areaType=Circle, applicableStats=[스킬 범위, 공격력, 스킬 유지 시간]

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
