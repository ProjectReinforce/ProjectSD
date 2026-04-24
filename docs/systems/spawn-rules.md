# Spawn Rules — 적 스폰·난이도 곡선 규약

> **SSOT:** 이 문서의 수치는 `Assets/Data/DifficultyData.asset` 과 `Assets/Data/GameplayConfig.asset` (SO)의 복제본이다.
> 밸런싱 수정은 **SO에서 먼저** 하고 이 문서는 그 결과를 반영한다.
> 참조 SO: `Assets/Data/DifficultyData.asset`, `Assets/Data/GameplayConfig.asset`
> 최종 동기화: 2026-04-24

적 스폰 타이밍/수량/스케일링의 SSOT. [../game-design/enemies/INDEX.md](../game-design/enemies/INDEX.md) 와 [../game-design/rules.md § 4](../game-design/rules.md) 에서 본 문서를 참조한다.

## 1. 메타

| 항목 | 값 |
|---|---|
| 시스템 ID | `spawn-rules` |
| 분류 | 전투 / 밸런싱 |
| 의존 레이어 | Adapter (SpawnManager) |
| 최종 업데이트 | 2026-04-24 |

## 2. 목적

시간 경과·인원수에 따라 적의 수량·스탯이 어떻게 변하는지를 수식·SO 곡선으로 고정. 구현은 `SpawnManager` 에서.

## 3. 시간 경과 곡선 (`DifficultyData`, 2인 기준)

**현재 구현은 구간 테이블이 아닌 `AnimationCurve` 기반 연속 보간.** 보스 스폰(900초) 시점을 `t=1.0` 으로 정규화.

| 수치 | `Start` | `End` | 곡선 | 설명 |
|---|---|---|---|---|
| 적 체력 배율 (`hp`) | **0.8x** | **15x** | `hpCurve` (linear 기본) | 보스까지 15배 상향 |
| 스폰 간격 (`interval`) | **1.5s** | **0.3s** | `intervalCurve` | 후반으로 갈수록 빠름 |
| 최대 동시 적 수 (`maxEnemy`) | **100** | **400** | `maxEnemyCurve` | 2인 기준 |
| 틱당 스폰 수 (`spawnPerTick`) | **2** | **10** | `spawnPerTickCurve` | 한 번의 스폰 틱에서 생성하는 마릿수 |
| 경험치 시간 배율 (`expTime`) | **1.2** | **0.4** | `expTimeCurve` | 초반 EXP 후함, 후반 박함 |

**게임 시간 0 ~ `GameplayConfig.totalGameTime`(900s) 을 `t ∈ [0,1]` 로 정규화** 하고 각 커브로 값을 보간한다. 곡선은 모두 linear(time=0→0, time=1→1) 가 기본. 튜닝 시 커브 편집으로 비선형화.

## 4. 적 타입별 등장 비율 (`DifficultyData`, 시작 → 종료)

| 타입 | 시작 | 종료 | 비고 |
|---|---|---|---|
| Chaser | **1.0 (100%)** | **0.3 (30%)** | `chaserRatio{Start,End}` |
| Runner | 0 | **0.25** | `runnerRatio{Start,End}` |
| Tank | 0 | **0.15** | `tankRatio{Start,End}` |
| Swarm | 0 | **0.30** | `swarmRatio{Start,End}` — 그룹 단위 스폰 |
| Ranged | **1.0** | **1.0** | `rangedRatio{Start,End}` — 4변형. SpawnManager.rangedVariants 중 랜덤 선택 |
| Elite | 독립 타이머 | 독립 타이머 | `SpawnManager.eliteSpawnInterval` (구현 측 상수). `eliteVariants` 중 랜덤 선택 |

*비율은 동일 시간대 내에서 정규화되어 사용. Ranged는 일반 타입과 **별도 축**으로 해석됨(코드 측 정책 확인 대상).*

**엘리트 스폰 정책:** 일반 스폰과 병행. 타이머 만료 시 `eliteVariants` 중 랜덤 1마리 추가. `maxEnemies` 상한 초과 시 해당 틱 스킵. `enableEliteSpawn` 토글 존재.

## 5. Swarm 그룹 설정 (`DifficultyData`)

| 필드 | 값 |
|---|---|
| `swarmGroupMin` | **5** |
| `swarmGroupMax` | **20** |
| `spawnOffsetMin` | 0.5 |
| `spawnOffsetMax` | 1.5 |
| `playerSafeZone` | 2 (이 반경 안에는 스폰 금지) |

## 6. 인원수 스케일링 (`DifficultyData.playerScalings`)

| 인원 | `healthMultiplier` | `maxEnemyMultiplier` | `expMultiplier` |
|---|---|---|---|
| 1 (솔로) | **0.6×** | **0.6×** | **1.0×** |
| 2 (기준) | 1.0× | 1.0× | 1.0× |
| 3 | 1.4× | 1.3× | 0.95× |
| 4 | 1.8× | 1.6× | 0.9× |

## 7. 스폰 위치 규칙

- 맵 경계의 랜덤 위치에서 스폰.
- `playerSafeZone` (반경 2) 안에는 스폰 금지.
- 스폰 틱마다 `spawnPerTick(t)` 마리 동시 등장 (무리형은 `swarmGroup[Min..Max]` 그룹 스폰).
- `maxEnemy(t, n)` 상한 도달 시 **스폰 중지**. 기존 적 처치 시 재개.

## 8. 수식

```
tNorm        = gameTime / GameplayConfig.totalGameTime          (clamp 0..1)
interval(t)  = lerp(intervalStart, intervalEnd, intervalCurve(tNorm))
maxEnemy(t,n)= lerp(maxEnemyStart, maxEnemyEnd, maxEnemyCurve(tNorm))
               × playerScaling[n].maxEnemyMultiplier
spawnTick(t) = lerp(spawnPerTickStart, spawnPerTickEnd, spawnPerTickCurve(tNorm))
enemyHp(t,n) = enemy.baseHP × lerp(hpStart, hpEnd, hpCurve(tNorm))
               × playerScaling[n].healthMultiplier
enemyExp(t,n)= enemy.expValue × playerScaling[n].expMultiplier
               × (expTime 은 시간 보상 주기 보정용, 별도 산식)
```

## 9. 데이터 출처

- `Assets/Data/DifficultyData.asset` — 곡선/인원 스케일링/Swarm 그룹
- `Assets/Data/GameplayConfig.asset` — `totalGameTime` 정규화 기준, `baseKnockbackForce` 등
- `Assets/Data/Enemy/**/*.asset` — 적별 base 스탯
- `Assets/Data/EnemyDropTable.asset` / `EliteDropTable.asset` — 드랍 확률

## 10. 네트워크

- **스폰 주체는 호스트.** 스폰 결정 후 RPC로 각 클라이언트에 전파.
- 기본 규약 [network-sync.md](network-sync.md).

## 11. 성능 / 제약

- 적 오브젝트 풀링 필수.
- 화면 밖 적 간소화 AI.
- 무리형 그룹 스폰 시 묶음 RPC 권장.

## 12. 기존 코드 참조

- `Assets/Scripts/Shared/Managers/SpawnManager.cs`
- `Assets/Scripts/Shared/Data/DifficultyData.cs`, `GameplayConfig.cs`

## 13. 알려진 제약 / 오픈 이슈

- [ ] Ranged 비율이 일반 타입과 별도 축으로 동작 — 실제 스폰 비중 정책 문서화 필요
- [ ] "플레이어 밀집" 판정 반경 재검증 (`playerSafeZone = 2`)
- [ ] 각 Curve 의 비선형화 튜닝 여부 (현재 모두 linear)
