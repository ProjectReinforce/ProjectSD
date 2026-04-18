# 스킬 설계서: 개미지옥 (Ant Hell)

## 1. 메타

| 항목 | 값 |
|---|---|
| 스킬 ID | `skill_ant_hell_01` |
| 한국어 이름 | 개미지옥 |
| 영어 이름 | Ant Hell |
| 카테고리 | 액티브 |
| 유형 | 장판 |
| 진화 여부 | Yes (나락) |
| 최종 업데이트 | 2026-04-18 |

## 2. 컨셉

지면에 설치하는 지속 피해 함정. 동선을 막아 적을 가두는 느낌. 진화형은 "체력이 낮은 적은 무조건 쓸어담는" 처형 스킬로 변모.

## 3. 기본 동작

| 항목 | 값 |
|---|---|
| 조준 | 주변 랜덤 위치 |
| 발사 모드 | DelayedBurst (초기에는 투사체 개수만큼 설치, 밸런싱에서 조정 가능) |
| 궤적 | — (장판) |
| 관통 | — |
| 투사체 개수 스탯 적용 | 초기 적용 (설치 수 증가). 밸런싱에서 재검토 |

**동작:** 랜덤 위치에 개미지옥 생성. 지나가는 적에게 지속 피해. 투사체 개수 스탯만큼 딜레이를 두고 연속 설치.

## 4. 수치

*실제 값은 `Assets/Data/Skills/ant_hell_01.asset`.*

| 레벨 | 틱 데미지 | 쿨다운 | 반경 | 지속시간 | 기타 |
|---|---|---|---|---|---|
| 1 | — | — | — | — | *TBD* |

## 5. TriggerEffect 매핑

| Trigger | EffectAction | 파라미터 | 용도 |
|---|---|---|---|
| OnInterval | DealDamage | (tick, radius, 0) | AreaZone 주기 피해 |

## 6. 진화 경로

- **진화 조건:** 개미지옥 + 공격력 증가
- **진화 후 이름:** 나락 (Abyss)
- **주요 변화:** 틱 피해 + **HP 비율 처형** (보스 제외).

**진화형 TriggerEffect:**

| Trigger | EffectAction | 파라미터 | 용도 |
|---|---|---|---|
| OnInterval | DealDamage | (tick, radius, 0) | 기본 틱 |
| OnInterval | Execute | (0.05, 0, 0) | HP ≤ 5% 즉사 (보스 제외) |

처형 기준값(기본 5%)은 **SO 설정 필수** — 하드코딩 금지.

**밸런싱 주의:**
- 틱 간격이 짧으면 처형 기준이 너무 높을 때 강력함 → 틱 간격을 늘리고 처형 기준을 높이는 방향 검토.

## 7. 데이터 계약

- **SO 타입:** `AreaSkillData`
- **에셋 경로:** `Assets/Data/Skills/ant_hell_01.asset`
- **주요 필드:** firingMode=DelayedBurst, areaType=Circle, tickInterval, executeThreshold(진화형, SO)

## 8. 네트워크

- 장판 설치 위치는 호스트 결정.
- 틱 판정/처형 판정은 호스트.

## 9. 구현 체크리스트

- [ ] SO 생성
- [ ] AreaZone 틱 간격 SO 튜닝
- [ ] 진화형 ExecuteHandler 연결, 보스 제외 로직 확인
- [ ] `photon-sync-auditor`
- [ ] 플레이테스트

## 10. 오픈 이슈

- 장판 설치 수 상한 (성능 관리, [skills/INDEX.md § 성능 관리](INDEX.md))
- 투사체 개수 적용 여부 최종 정책 (밸런싱)
- 처형 기준값 기본값 5%가 적절한지 (밸런싱)
