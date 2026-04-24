# 적 설계서: 무리형 (Swarm)

> **SSOT:** 이 문서의 수치는 `Assets/Data/Enemy/SwarmData.asset` 과 `Assets/Data/DifficultyData.asset` (그룹 사이즈)의 복제본이다.
> 밸런싱 수정은 **SO에서 먼저** 하고 이 문서는 그 결과를 반영한다.
> 참조 SO: `Assets/Data/Enemy/SwarmData.asset`, `Assets/Data/DifficultyData.asset`
> 최종 동기화: 2026-04-24

## 1. 메타

| 항목 | 값 |
|---|---|
| 엔티티 ID | `enemy_swarm` |
| 한국어 이름 | 무리형 |
| 영어 이름 | Swarm |
| `enemyType` | `Swarm` (3) |
| 분류 | 기본 |
| 등장 시점 | 0분~ |
| 등장 비율 | 시작 0% → 종료 30% (DifficultyData 곡선) |
| 최종 업데이트 | 2026-04-24 |

## 2. 컨셉

한 방향으로만 이동하며 그룹 단위로 등장. 이동 경로 예측을 요구하여 회피 가능. 플레이어 추적 없음.

## 3. 스탯 (현재 SO 값)

| 필드 | 값 |
|---|---|
| `baseHP` | **15** |
| `contactDamage` | **10** |
| `moveSpeed` | **0.55** (Unity m/s) |
| `expValue` | **2** (개별, 그룹이 전멸하면 누적 많음) |
| `knockbackResistance` | 0 |
| `resolveOverlap` | **false** (Anti-Overlap 비활성) |

**그룹 사이즈 (`DifficultyData`)**
| 필드 | 값 |
|---|---|
| `swarmGroupMin` | **5** |
| `swarmGroupMax` | **20** |
| `spawnOffsetMin` | 0.5 |
| `spawnOffsetMax` | 1.5 |
| `playerSafeZone` | 2 |

## 4. 이동 패턴

- **이동 타입:** SwarmMovement (랜덤 방향 직진)
- **참조:** `Assets/Scripts/Features/Enemy/Adapter/Movement/`
- **특수 동작:** 일정 시간 직진 후 소멸. **5~20마리 그룹 스폰** (`SpawnManager` 가 한 번에 그룹 단위 생성).
- **겹침 허용:** `resolveOverlap = false` 로 Anti-Overlap(`EnemyMovement.ResolveEnemyOverlap`) 을 비활성화. 밀집 돌진이 컨셉이라 서로 겹쳐도 되고, 그룹 연산 비용도 절약됨. 다른 일반 적은 기본값 `true` 유지.

## 5. 공격 패턴

접촉 데미지.

## 6. 보상

- **경험치:** 2 개별.
- **드랍:** `EnemyDropTable.asset` 공용.

## 7. 데이터 계약

- **SO 타입:** `EnemyData`
- **에셋 경로:** `Assets/Data/Enemy/SwarmData.asset`
- **주요 필드:** `resolveOverlap = false`. 그룹 사이즈는 `DifficultyData.swarmGroup{Min,Max}` 에서 관리(전역).

## 8. 네트워크

- 그룹 스폰 시 묶음 RPC로 효율화 권장.

## 9. 체크리스트

- [x] SO 생성
- [x] SwarmMovement 재사용
- [ ] 그룹 스폰 로직 (묶음 RPC)
- [ ] `photon-sync-auditor`
- [ ] 플레이테스트

## 10. 오픈 이슈

- 직진 방향 결정 로직 (완전 랜덤 vs 맵 경계 기준 안쪽)
- 그룹이 한꺼번에 터질 때의 성능/이펙트 폭증 (풀링) — Anti-Overlap 비활성화로 겹침 연산 부하는 제거됨
