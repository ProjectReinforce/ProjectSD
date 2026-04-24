# 스킬 설계서: 성역 (Sanctuary)

## 1. 메타

| 항목 | 값 |
|---|---|
| 스킬 ID | 7 (`skillId`) |
| 한국어 이름 | 성역 |
| 영어 이름 | Sanctuary (파일명: `007_HollyArea`) |
| 카테고리 | 액티브 |
| 유형 | 장판 (회복) |
| 진화 여부 | Yes (심판의 성역) |
| 최종 업데이트 | 2026-04-24 |

> **SSOT:** 이 문서의 수치는 `Assets/Data/Skill/Active/007_HollyArea.asset` 의 복제본이다.
> (파일명의 "Holly" 는 "Holy" 오타로 추정 — 추후 리네이밍 검토)

## 2. 컨셉

플레이어 위치 고정 회복 구역. 팀 서포트 역할. 진화형은 회복에 공격 시너지를 결합해 "심판"으로 변모.

## 3. 기본 동작

| 항목 | 값 |
|---|---|
| 조준 | 플레이어 위치 (고정) |
| 발사 모드 | Single (무조건 1개) |
| 궤적 | — (장판) |
| 관통 | — |
| 투사체 개수 스탯 적용 | **적용 안 됨** (무조건 1개) |

**동작:** 플레이어 위치에 회복 구역 생성. 틱 간격마다 플레이어 회복. 회복량은 패시브 스탯(체력 회복량 증가) 반영.

## 4. 수치 (현재 SO 값)

### 4.1 레벨별

| 레벨 | 틱 회복량 (`damagePerLevel`) | 쿨다운 |
|---|---|---|
| 1 | **5** | **6.0s** |
| 2 | 6 | 5.5s |
| 3 | 8 | 5.0s |
| 4 | 10 | 4.5s |
| 5 | 12 | 4.0s |
| 6 | 15 | 3.5s |
| 7 | **18** | **3.0s** |

### 4.2 발사 파라미터

| 필드 | 값 |
|---|---|
| `firingMode` | Single (3) |
| `areaRadius` | **0.5** |
| `areaDuration` | **2초** |
| `tickRate` | 0.5초 |
| `isHealingEffect` | **true** (힐링 적용) |
| `maxInstances` | 2 |

## 5. TriggerEffect 매핑

| Trigger | EffectAction | 파라미터 | 용도 |
|---|---|---|---|
| OnInterval | HealSelf | (heal, 0, 0) | 팀원 회복 |

## 6. 진화 경로

- **진화 조건:** 성역 + 체력 회복량 증가
- **진화 후 이름:** 심판의 성역 (Judgment Sanctuary)
- **주요 변화:** 기본 회복 + **구역 내 적이 받은 데미지 합산의 일정 비율만큼 추가 회복**.

**예시 계산:**
```
기본 회복 10 + (적 5마리 × 각 데미지 10 = 합산 50) × ratio
```

**진화형 TriggerEffect (설계):**

| Trigger | EffectAction | 파라미터 | 용도 |
|---|---|---|---|
| OnInterval | HealSelf | (heal, 0, 0) | 기본 회복 |
| OnHit | HealSelf | (0, damageRatio, 0) | 적 데미지 비율 회복 |
| OnInterval | DealDamage | (tick, radius, 0) | 적 지속 피해 |

> HealSelf 의 `secondary` 가 데미지 비율 회복 (context.damage × ratio). [trigger-effects.md § 3.9](../../systems/trigger-effects.md).

## 7. 데이터 계약

- **SO 타입:** `ProjectileSkillData` (장판 힐링 모드)
- **에셋 경로:** `Assets/Data/Skill/Active/007_HollyArea.asset`
- **evolvedSkill:** `207_EvolvedHollyArea.asset`
- **주요 필드:** firingMode=Single, isHealingEffect=true, applicableStats=[스킬 범위, 체력 회복량 증가, 스킬 유지 시간]

## 8. 네트워크

- Single 이므로 호스트 판정 단순.
- 멀티에서 모든 팀원에게 회복 적용 로직은 호스트에서.

## 9. 구현 체크리스트

- [ ] SO 생성
- [ ] Single 모드로 투사체 개수 스탯 무시 확인
- [ ] 진화형 데미지 비율 회복 수치 튜닝
- [ ] `photon-sync-auditor`
- [ ] 플레이테스트

## 10. 오픈 이슈

- 기본 틱 간격·회복량 밸런싱 (과다한 느낌 보고됨)
- 진화형 데미지 비율이 팀원 데미지를 모두 합산하는지, 소유자 것만 합산하는지
