# 적 설계서: 기본 추적형 (Chaser)

> **SSOT:** 이 문서의 수치는 `Assets/Data/Enemy/ChaserData.asset` (SO)의 복제본이다.
> 밸런싱 수정은 **SO에서 먼저** 하고 이 문서는 그 결과를 반영한다.
> 참조 SO: `Assets/Data/Enemy/ChaserData.asset`, `Assets/Data/DifficultyData.asset`
> 최종 동기화: 2026-04-24

## 1. 메타

| 항목 | 값 |
|---|---|
| 엔티티 ID | `enemy_basic_chaser` |
| 한국어 이름 | 기본 추적형 |
| 영어 이름 | Chaser |
| `enemyType` | `Chaser` (0) |
| 분류 | 기본 |
| 등장 시점 | 0분~ |
| 등장 비율 | 시작 100% → 종료 30% (DifficultyData 곡선) |
| 최종 업데이트 | 2026-04-24 |

## 2. 컨셉

가장 많이 등장하는 기본 적. 플레이어를 단순 추적. 프로토타입의 기준선 역할.

## 3. 스탯 (현재 SO 값)

| 필드 | 값 |
|---|---|
| `baseHP` | **50** |
| `contactDamage` | **20** |
| `moveSpeed` | **0.6** (Unity m/s) |
| `expValue` | **5** |
| `knockbackResistance` | 0 |
| `visualScaleMultiplier` | 1 |
| `resolveOverlap` | true |

**시간 경과 체력 배율:** `DifficultyData.hpStart` **0.8** → `hpEnd` **15** (0~`bossSpawnTime` 900s, curve에 따른 보간).
**인원 스케일링:** 1인 ×0.6 / 2인 ×1.0 / 3인 ×1.4 / 4인 ×1.8 (DifficultyData.playerScalings). 상세 [systems/spawn-rules.md](../../systems/spawn-rules.md).

## 4. 이동 패턴

- **이동 타입:** ChaseMovement (가장 가까운 플레이어 직선 추적)
- **참조:** `Assets/Scripts/Features/Enemy/Adapter/Movement/`
- **특수 동작:** 접촉 시 데미지 + 넉백.

## 5. 공격 패턴

접촉 데미지만. 원거리 패턴 없음.

## 6. 보상

- **경험치:** 5 (`expValue`)
- **드랍:** `EnemyDropTable.asset` 참조 — magnetChance 0.01 / potionChance 0.01 / weaponChance 1 / essenceChance 0.

## 7. 데이터 계약

- **SO 타입:** `EnemyData`
- **에셋 경로:** `Assets/Data/Enemy/ChaserData.asset`

## 8. 네트워크

기본 규약 [systems/network-sync.md](../../systems/network-sync.md).

- 스폰/AI 호스트, 위치/체력/상태 전송
- 클라이언트 보간

## 9. 체크리스트

- [x] SO 생성 (`ChaserData.asset`)
- [x] ChaseMovement 연결
- [ ] 넉백 로직 확인
- [ ] `photon-sync-auditor`
- [ ] 플레이테스트

## 10. 오픈 이슈

- 극초반 특수 처리 (너무 빠르게 덮치지 않도록)
