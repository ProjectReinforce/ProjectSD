# 적 설계서: 둔한형 (Tank)

> **SSOT:** 이 문서의 수치는 `Assets/Data/Enemy/TankData.asset` (SO)의 복제본이다.
> 밸런싱 수정은 **SO에서 먼저** 하고 이 문서는 그 결과를 반영한다.
> 참조 SO: `Assets/Data/Enemy/TankData.asset`
> 최종 동기화: 2026-04-24

## 1. 메타

| 항목 | 값 |
|---|---|
| 엔티티 ID | `enemy_tank` |
| 한국어 이름 | 둔한형 |
| 영어 이름 | Tank |
| `enemyType` | `Tank` (2) |
| 분류 | 기본 |
| 등장 시점 | 0분~ |
| 등장 비율 | 시작 0% → 종료 15% (DifficultyData 곡선) |
| 최종 업데이트 | 2026-04-24 |

## 2. 컨셉

느리지만 높은 체력으로 경로를 차단. 밀어내기 저항으로 장검·장풍 같은 CC 스킬을 흘려보낸다.

## 3. 스탯 (현재 SO 값)

| 필드 | 값 |
|---|---|
| `baseHP` | **150** |
| `contactDamage` | **30** |
| `moveSpeed` | **0.3** (Unity m/s — Chaser 0.6의 절반) |
| `expValue` | **12** |
| `knockbackResistance` | **0.5** (넉백 50% 저항) |
| `visualScaleMultiplier` | 1 |
| `resolveOverlap` | true |

시간/인원 스케일링은 Chaser와 동일.

## 4. 이동 패턴

- **이동 타입:** ChaseMovement (저속 추적)
- **특수 동작:** **넉백 50% 저항** (`knockbackResistance = 0.5`). 장검/장풍 등 밀어내기 스킬의 이동량이 절반.

## 5. 공격 패턴

접촉 데미지 (높음, 30).

## 6. 보상

- **경험치:** 12
- **드랍:** `EnemyDropTable.asset` 공용.

## 7. 데이터 계약

- **SO 타입:** `EnemyData`
- **에셋 경로:** `Assets/Data/Enemy/TankData.asset`
- **주요 필드:** `knockbackResistance = 0.5`

## 8. 네트워크

- 저속이라 동기화 부담 적음.

## 9. 체크리스트

- [x] SO 생성
- [x] 넉백 저항 구현
- [ ] 패시브 "넉백 거리 증가" 와의 상쇄 확인
- [ ] `photon-sync-auditor`
- [ ] 플레이테스트

## 10. 오픈 이슈

- 경로 차단 효과가 재미와 불편 중 어느 쪽인지 플레이테스트 필요
