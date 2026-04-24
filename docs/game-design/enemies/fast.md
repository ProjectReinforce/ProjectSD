# 적 설계서: 빠른형 (Runner)

> **SSOT:** 이 문서의 수치는 `Assets/Data/Enemy/RunnerData.asset` (SO)의 복제본이다.
> 밸런싱 수정은 **SO에서 먼저** 하고 이 문서는 그 결과를 반영한다.
> 참조 SO: `Assets/Data/Enemy/RunnerData.asset`
> 최종 동기화: 2026-04-24

## 1. 메타

| 항목 | 값 |
|---|---|
| 엔티티 ID | `enemy_fast_runner` |
| 한국어 이름 | 빠른형 |
| 영어 이름 | Runner |
| `enemyType` | `Runner` (1) |
| 분류 | 기본 |
| 등장 시점 | 0분~ |
| 등장 비율 | 시작 0% → 종료 25% (DifficultyData 곡선) |
| 최종 업데이트 | 2026-04-24 |

## 2. 컨셉

저체력·고속. 이동 판단 실수를 유도. 범위 공격에 취약하지만 접근이 빨라 당황하기 쉽다.

## 3. 스탯 (현재 SO 값)

| 필드 | 값 |
|---|---|
| `baseHP` | **25** |
| `contactDamage` | **15** |
| `moveSpeed` | **0.75** (Unity m/s — Chaser 0.6 대비 +25%) |
| `expValue` | **3** |
| `knockbackResistance` | 0 |
| `visualScaleMultiplier` | 1 |
| `resolveOverlap` | true |

시간/인원 스케일링은 Chaser와 동일(`DifficultyData` 공용).

## 4. 이동 패턴

- **이동 타입:** ChaseMovement (고속 추적)
- **특수 동작:** 없음. 낮은 체력 상쇄.

## 5. 공격 패턴

접촉 데미지만.

## 6. 보상

- **경험치:** 3 (`expValue`)
- **드랍:** `EnemyDropTable.asset` 공용.

## 7. 데이터 계약

- **SO 타입:** `EnemyData`
- **에셋 경로:** `Assets/Data/Enemy/RunnerData.asset`

## 8. 네트워크

- 고속 이동 → 보간 품질이 중요. 지연 경고 기준은 추후 결정.

## 9. 체크리스트

- [x] SO 생성
- [x] 이동속도 0.75 튜닝
- [ ] `photon-sync-auditor`
- [ ] 플레이테스트

## 10. 오픈 이슈

- 근접 스킬로는 잡기 어려움 — 의도 vs 불만 사이 밸런스
