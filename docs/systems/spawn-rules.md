# Spawn Rules — 적 스폰·난이도 곡선 규약

적 스폰 타이밍/수량/스케일링의 SSOT. [../game-design/enemies/INDEX.md](../game-design/enemies/INDEX.md) 와 [../game-design/rules.md § 4](../game-design/rules.md) 에서 본 문서를 참조한다.

## 1. 메타

| 항목 | 값 |
|---|---|
| 시스템 ID | `spawn-rules` |
| 분류 | 전투 / 밸런싱 |
| 의존 레이어 | Adapter (SpawnManager) |
| 최종 업데이트 | 2026-04-18 |

## 2. 목적

시간 경과·인원수에 따라 적의 수량·스탯이 어떻게 변하는지를 수식·테이블로 고정. 구현은 `SpawnManager` 에서.

## 3. 시간대별 스폰 테이블 (2인 기준)

| 구간 | 시간 | 스폰 간격 (초) | 동시 적 수 (최대) | 체력 배율 | 특징 |
|---|---|---|---|---|---|
| 초반 | 0~3분 | 2.5 | 30 | 1.0x | 약한 적, 빌드 구축 시간 |
| 중반 1 | 3~5분 | 2.0 | 50 | 1.3x | 스폰 속도 증가 |
| 중반 2 | 5~7분 | 1.5 | 70 | 1.6x | 밀집도 상승 |
| 후반 | 7~10분 | 1.2 | 90 | 2.0x | 최대 난이도 |

> **보스 등장 시점: 현재 15분 기준** (밸런싱에서 10분으로 단축 가능). 10분으로 단축 시 본 테이블의 "후반" 구간이 단축 또는 삭제되어야 함.

## 4. 적 타입별 등장 비율 (시간대 무관, 2인 기준)

| 타입 | 비율 | 비고 |
|---|---|---|
| 기본 추적형 (Chaser) | 60% | |
| 빠른형 (Runner) | 20% | |
| 둔한형 (Tank) | 10% | |
| 무리형 (Swarm) | 10% | 그룹 단위 스폰 |
| 원거리형 (Ranged) | `DifficultyData.rangedRatio{Start,End}` (기본 0) | 4변형([enemies/ranged.md](../game-design/enemies/ranged.md)). SpawnManager.rangedVariants 배열에서 랜덤 선택. 비율 밸런싱 TBD |
| 엘리트형 (Elite) | `SpawnManager.eliteSpawnInterval` (기본 90s) | [enemies/elite.md](../game-design/enemies/elite.md). 일반 비율과 독립된 타이머로 스폰. `SpawnManager.eliteVariants` 배열에서 랜덤 선택 |

**엘리트 스폰 정책 (구현 반영):** 일반 스폰과 병행 동작. 타이머가 만료될 때마다 `eliteVariants` 중 랜덤 1마리 추가. 동시 적 수 상한(`maxEnemies`)을 넘으면 해당 틱 스킵 (다음 타이머까지 대기). `enableEliteSpawn` 토글로 전체 끄기 가능.

## 5. 인원수 스케일링

| 인원 | 체력 배율 | 동시 적 수 배율 | 경험치 배율 |
|---|---|---|---|
| 1 (솔로) | 0.6x | 0.6x | 1.0x |
| 2 (기준) | 1.0x | 1.0x | 1.0x |
| 3 | 1.4x | 1.3x | 0.95x |
| 4 | 1.8x | 1.6x | 0.9x |

## 6. 스폰 위치 규칙

- 맵 경계의 랜덤 위치에서 스폰.
- **플레이어 밀집 지역 근처는 스폰 금지** (최소 15m 거리).
- 스폰 간격마다 1~3마리 동시 등장 (무리형은 5~10마리 그룹 스폰).
- 동시 적 수 상한 도달 시 **스폰 중지**. 기존 적 처치 시 재개.

## 7. 수식

```
spawnInterval(t) = table.spawnInterval(phaseOf(t))
maxSimultaneous(t, n) = table.maxConcurrent(phaseOf(t)) × playerCountMult(n).maxEnemies
enemyHp(t, n) = enemyBaseHp × hpTimeMult(phaseOf(t)) × playerCountMult(n).hp
enemyExp(t, n) = enemyBaseScore × playerCountMult(n).exp
```

- `phaseOf(t)`: 현재 게임 시간이 어느 구간(초반/중반1/중반2/후반)인지
- `playerCountMult(n)`: 인원 n의 배율 표

## 8. 데이터 출처

- `Assets/Data/DifficultyData.asset` (예정) — 구간별 수치
- `Assets/Data/Enemies/*.asset` — 적별 base 스탯
- `Assets/Data/GameplayConfig.asset` — 인원 스케일링 배율

## 9. 네트워크

- **스폰 주체는 호스트.** 스폰 결정 후 RPC로 각 클라이언트에 `enemyId`, `position`, `spawnTime` 전파.
- 기본 규약 [network-sync.md](network-sync.md).

## 10. 성능 / 제약

- 적 오브젝트 풀링 필수 (최대 100개 풀 권장).
- 화면 밖 적 간소화 AI, 업데이트 주기 감소(20Hz → 10Hz).
- 무리형 그룹 스폰 시 묶음 RPC 권장 (패킷 효율).

## 11. 기존 코드 참조

- `Assets/Scripts/Adapter/Manager/SpawnManager.cs` (예정/진행 중)
- `Assets/Scripts/Data/DifficultyData.cs`, `GameplayConfig.cs`

## 12. 알려진 제약

- [ ] 후반 이후(10분 이후) 테이블 미정 — 보스 타이머 확정 후 채우기
- [ ] 원거리형/엘리트형 등장 시점 미정
- [ ] "플레이어 밀집" 판정 반경(15m) 의 밸런싱 재검증
