# Enemy Stat Scaling — 적 스탯 시간/인원 스케일링

> **SSOT:** 본 문서가 적 스탯 곡선·민감도(sensitivity)·인원 스케일링 차원의 SSOT. 스폰 빈도/수량/타입 비율은 [spawn-rules.md](spawn-rules.md) 가 담당.
> 참조 SO: `Assets/Data/DifficultyData.asset`, `Assets/Data/Enemy/**/*.asset`
> 최종 업데이트: 2026-05-01

## 1. 메타

| 항목 | 값 |
|---|---|
| 시스템 ID | `enemy-stat-scaling` |
| 분류 | 전투 / 밸런싱 |
| 의존 레이어 | Adapter (SpawnManager, Enemy, EnemyMovement) + Data (DifficultyData, EnemyData) |
| 상태 | ✅ 구현 완료 (2026-05-01, 커밋 `1e4c373fb`) — 사용자 후속: EnemyData 12개 .asset 의 sensitivity 인스펙터 채우기(현재 default 0.5 단일) |
| 최종 업데이트 | 2026-05-01 |

## 2. 목적

기존에는 시간 경과에 따라 곡선 적용되는 적 스탯이 **HP 단 하나** 였다. 후반부 적은 "맞아도 안 죽는다" 만 강해질 뿐, 더 빠르거나 더 아프게 때리지는 않았기 때문에 후반의 단조로움이 두드러졌다.

본 시스템은 다음을 도입한다.

1. **데미지 시간 배율** — 적 `contactDamage`/`attackDamage` 가 시간에 따라 곡선으로 증가
2. **이동속도 시간 배율 + 타입별 민감도(sensitivity)** — 시간에 따라 이속이 빨라지되, 타입 정체성을 보존하기 위해 적별로 반영 비율을 0~1 로 차등
3. **인원수 스케일링 확장** — `damageMultiplier`, `moveSpeedMultiplier` 추가. HP 보다 약한 비율로 적용하여 4인 파티에서 일격사 페널티 완화

방어력/속성 저항은 본 시스템 범위 외 (보류 — 본 문서 § 9 참조).

## 3. 인터페이스 (예정)

```csharp
public class DifficultyManager
{
    // 신규
    public float GetDamageMultiplier(float gameTime);                       // 시간 t 의 데미지 배율
    public float GetMoveSpeedMultiplier(float gameTime);                    // 시간 t 의 이속 배율 (raw, sensitivity 미적용)
    public float GetDamageMultiplier(float gameTime, int playerCount);      // 위 + PlayerScaling.damageMultiplier
    public float GetMoveSpeedMultiplier(float gameTime, int playerCount);   // 위 + PlayerScaling.moveSpeedMultiplier
}
```

호출 위치 예정:
- `SpawnManager` — 스폰 시점에 시간/인원 배율 조회 → `Enemy.Initialize` 인자로 전달
- `Enemy.Initialize` — 시그니처 확장: `damageMul`/`speedMul` 인자 추가, 내부에서 `EnemyData.moveSpeedScaleSensitivity` 적용 후 최종값 캐싱
- `EnemyMovement` — 캐싱된 `finalMoveSpeed` 만 사용 (별도 sensitivity 계산 없음)
- `EnemyAttack` / `EnemyContact` — 캐싱된 `finalContactDamage`/`finalAttackDamage` 사용

## 4. 공식

### 4.1 시간 배율 (`DifficultyData` 곡선)

```
tNorm     = clamp01(gameTime / GameplayConfig.bossSpawnTime)
dmgMul(t) = lerp(damageStart,    damageEnd,    damageCurve(tNorm))
spdMul(t) = lerp(moveSpeedStart, moveSpeedEnd, moveSpeedCurve(tNorm))
```

### 4.2 이속 — 타입별 민감도 보간

```
sens          = EnemyData.moveSpeedScaleSensitivity (0 ~ 1)
effectiveSpd  = Lerp(1.0, spdMul(t), sens)
finalMoveSpd  = EnemyData.moveSpeed × effectiveSpd × PlayerScaling.moveSpeedMultiplier
```

**핵심:** `Lerp(1, spdMul, sens)` 형태이므로 `sens=0` 이면 시간배율 영향 0%, `sens=1` 이면 100% 반영. 직접 `spdMul × sens` 가 아니라 **1과 spdMul 사이를 sens 만큼 보간**해야 sens=0 인 Tank가 시간배율의 영향을 받지 않는다 (직접 곱하면 sens=0 일 때 이속이 0 이 되어버림).

### 4.3 데미지 — 전 타입 동일 적용 (Phase 1)

```
finalContactDmg = EnemyData.contactDamage × dmgMul(t) × PlayerScaling.damageMultiplier
finalAttackDmg  = EnemyData.attackDamage  × dmgMul(t) × PlayerScaling.damageMultiplier
```

데미지에는 타입별 sensitivity 를 도입하지 않는다. 노브 수가 너무 많아져 튜닝 비용이 늘고, 타입 정체성은 base 값(예: Tank 30 vs Runner 15)으로 이미 충분히 표현되기 때문. 후속 필요 시 `damageScaleSensitivity` 를 동일 패턴으로 추가 가능.

### 4.4 적용 시점

- **계산은 스폰 1회.** `SpawnManager` 가 스폰 시 `gameTime` 으로 배율을 조회 → `Enemy.Initialize` 에 전달 → Enemy 내부에서 final 값 캐싱.
- **런타임 재계산 없음.** 후반에 스폰된 적은 시간 t 의 강화를 갖고 등장, 사망까지 그 값 유지. (후속 강화는 매 프레임 lookup 비용 + 네트워크 동기화 복잡도 ↑ 라 채택 안 함.)
- **반올림:** 데미지는 정수 필드라 `Mathf.RoundToInt` (HP 와 동일 정책). 이속은 float 그대로.

## 5. 권장 곡선/계수

### 5.1 `DifficultyData` 신규 필드

| 필드 | start | end | 곡선 기본 | 비고 |
|---|---|---|---|---|
| `damageStart` / `damageEnd` / `damageCurve` | **1.0** | **3.0** | `EaseInOut(0,0,1,1)` | HP 25배만큼 키우면 후반 즉사 — 3배 보수적 시작 |
| `moveSpeedStart` / `moveSpeedEnd` / `moveSpeedCurve` | **1.0** | **1.6** | `EaseInOut(0,0,1,1)` | 60% 가속. 2.0+ 면 Runner 추격 불가 |

### 5.2 `EnemyData` 신규 필드

| 필드 | 타입 | 권장 기본 |
|---|---|---|
| `moveSpeedScaleSensitivity` | `[Range(0,1)] float` | 0.5 |

**적별 권장값:**

| 적 | sensitivity | timeMul=1.6 시 effectiveSpd | base 0.6 → 후반 final |
|---|---|---|---|
| Tank | **0.0** | 1.00 | 0.30 (변화 없음) |
| Chaser | **0.5** | 1.30 | 0.78 |
| Swarm | **0.7** | 1.42 | 0.78 |
| Runner | **1.0** | 1.60 | 1.20 |
| Ranged | **0.3** | 1.18 | (ranged base × 1.18) |
| Elite | base 동일 (개별 SO 에서 결정) | — | — |

설계 의도: Runner 와 Tank 의 후반 격차를 더 벌려 타입 정체성 강화.

### 5.3 `PlayerScaling` 신규 필드

| 인원 | `healthMul` (기존) | `damageMul` (신규) | `moveSpeedMul` (신규) | `maxEnemyMul` (기존) | `expMul` (기존) |
|---|---|---|---|---|---|
| 1 | 0.6 | **0.7** | **1.0** | 0.6 | 1.0 |
| 2 (기준) | 1.0 | **1.0** | **1.0** | 1.0 | 1.0 |
| 3 | 1.4 | **1.15** | **1.05** | 1.3 | 0.95 |
| 4 | 1.8 | **1.3** | **1.1** | 1.6 | 0.9 |

설계 의도: HP 는 4명이 분담하므로 1.8× 가 자연스러우나, 데미지/이속은 한 명에게 그대로 꽂히는 차원이라 같은 비율을 쓰면 캐주얼층에 가혹. **HP 비율의 절반 ~ 1/3 정도를 적용**한다. 솔로(1인)의 데미지 배율 0.7 은 솔로 모드 부담 완화 — 베타 후 튜닝.

## 6. 데이터 출처

- `Assets/Data/DifficultyData.asset` — `damage*`, `moveSpeed*` 곡선 + `playerScalings[].damageMultiplier`/`moveSpeedMultiplier`
- `Assets/Data/Enemy/**/*.asset` — `moveSpeedScaleSensitivity`
- `Assets/Data/GameplayConfig.asset` — `bossSpawnTime` 정규화 기준

## 7. 네트워크

- **계산 주체:** 호스트만. `SpawnManager` 가 스폰 시 배율 적용 후 → 스폰 RPC 에 final 값 또는 결정 입력(스폰 시각 등)을 포함.
- **방안 비교:**
  - (A) RPC 에 `damageMul`/`speedMul` float 2개 추가 — 단순. 추천.
  - (B) 클라가 `gameTime` + `playerCount` 으로 자체 계산 — 동기화 risk. 두 측이 다른 시각에 평가하면 미세 발산.
- **권장:** (A). 페이로드 부담 없음 (float 2개), Source-of-truth 명확.
- 기본 규약은 [network-sync.md](network-sync.md).

## 8. 테스트

- **단위 테스트 후보:** `DifficultyManager.GetDamageMultiplier(t)` / `GetMoveSpeedMultiplier(t)` — `t=0`/`0.5`/`1` 에서 기대값.
- **플레이 모드 시나리오:**
  - 0초/450초/900초 시점 스폰된 같은 적의 최종 HP/데미지/이속 비교
  - Tank 가 후반에도 base 이속 유지하는지 (sens=0 검증)
  - Runner 가 후반에 1.6배 빠른지 (sens=1.0 검증)
  - 4인 파티에서 인원 배율 곱 적용 확인
- **회귀 체크:**
  - 보스 본체 스탯에는 영향 없어야 함 (보스는 별도 페이즈 시스템). `BossSpawner` 가 본 시스템을 우회하는지 확인 필요.
  - 엘리트는 본 시스템 적용 여부 결정 필요 (현재 권장: 적용).

## 9. 알려진 제약 / 트레이드오프

- [x] **방어력 (armor) 미도입** — Survivors-like 의 다중 약공격 빌드를 죽이는 함정. 도입 시 빌드 다양성 ↓. 보류.
- [x] **속성 저항 미도입** — 정수 시스템(얼음/불/번개)의 종류가 적어 차별화 효과 작음. 정수 종류 확장 후 별건으로 도입 검토.
- [x] **타입별 데미지 sensitivity 미도입** — 노브 수 증가 vs 효과 비례 검토 후 Phase 1 에서는 제외. base 값 차이로 이미 표현됨.
- [ ] **스폰 시점 1회 평가** — 후반에 스폰된 적이 평생 그 강도 유지. 매우 긴 스폰 간격(분 단위)에서는 자연스러우나, 스폰 후 사용자 행동에 따라 적이 오래 살아남으면 "후반 적인데 약함" 발생 가능. 현재 적 평균 수명을 고려하면 무시 가능 수준. 만약 문제 시 → 매 N초 재평가하는 strategy 추가.
- [ ] **부동소수점 오차** — `damageMul × baseContactDamage` 의 RoundToInt 결과가 호스트/클라 간 차이날 수 있음. 호스트가 결정한 값을 RPC 로 전파하는 방안 (A) 채택으로 회피.

## 10. 변경 이력

- 2026-05-01: 초안 작성 (Hyeon-Woo 브랜치). 미구현, spec only.
- 2026-05-01: 구현 완료 (커밋 `1e4c373fb`). 5파일(DifficultyData/DifficultyManager/EnemyData/Enemy/SpawnManager) 122줄+ / 29줄−. 코드 default 가 spec § 5 권장값과 일치 → runtime 즉시 동작. 사용자 후속(별건): 적별 sensitivity 차별화 (Tank 0 / Runner 1.0 등) 인스펙터 채우기.
