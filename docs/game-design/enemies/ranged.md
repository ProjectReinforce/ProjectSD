# 적 설계서: 원거리형 (Ranged)

## 1. 메타

| 항목 | 값 |
|---|---|
| 엔티티 ID (베이스) | `enemy_ranged` |
| 한국어 이름 | 원거리형 |
| 영어 이름 | Ranged |
| 분류 | 기본 (원거리) |
| 등장 시점 | TBD (밸런싱) |
| 등장 비율 | TBD |
| 최종 업데이트 | 2026-04-18 |

## 2. 컨셉

원거리에서 공격해 플레이어의 이동 동선을 제약하는 타입. 근접 적이 플레이어를 압박하는 동안 뒤에서 위협을 얹어 밀집·이동 타이밍을 강제한다.

## 3. 변형 (Variants)

프로토타입에서는 **2가지 행동 변형** + **2가지 공격 방식** 조합. 추후 확장 가능.

### 3.1 행동 변형

| 변형 ID | 이름 | 설명 |
|---|---|---|
| `ranged_stationary` | 고정형 | **스폰된 자리에서 움직이지 못함.** 플레이어가 사거리 안에 있으면 원거리 공격만 반복 |
| `ranged_kite` | 추격형 | 플레이어가 **사거리 안으로 들어올 때까지 추적**. 사거리 도달 시 정지 후 공격. 공격 후 다시 필요하면 추격 재개 |

### 3.2 공격 방식

| 공격 ID | 이름 | 설명 |
|---|---|---|
| `attack_projectile` | 투사체 발사 | 플레이어 방향으로 투사체 1발 발사. 투사체는 일정 속도·사거리 보유. 플레이어가 피할 수 있음 |
| `attack_telegraph` | 경고 비주얼 | 대상 지점에 **경고 표시(원/선) → 일정 시간 후 데미지**. 피하려면 경고 시간 내 이탈 필요 |

### 3.3 조합

4가지 조합이 가능하며, 각각 별도 SO 로 관리:

| 조합 | SO ID | 특징 |
|---|---|---|
| 고정형 + 투사체 | `enemy_ranged_turret_shot` | 포탑형. 투사체 회피 가능, 사거리 밖으로 빠지면 안전 |
| 고정형 + 경고 비주얼 | `enemy_ranged_turret_zone` | 위치 경고형. 플레이어 위치 예측 공격. 밀집 플레이어에 강함 |
| 추격형 + 투사체 | `enemy_ranged_kite_shot` | 저격형. 추적하다 멈추고 사격, 거리 유지 플레이 유도 |
| 추격형 + 경고 비주얼 | `enemy_ranged_kite_zone` | 붙어와서 경고 공격. 회피 난이도 높음 |

## 4. 스탯 (기준값, 2인·레벨 1)

*실제 값은 각 SO 에서. 아래는 설계 의도 기록.*

| 필드 | 추천 범위 | 비고 |
|---|---|---|
| HP | 80~120 | 기본 추적형과 유사하거나 약간 낮게 |
| 데미지 | 15~25 | 접촉 적보다 높아 이동 강제 |
| 이속 (추격형) | 2.5~3.0 | 기본 추적형보다 약간 느림 |
| 이속 (고정형) | 0 | 이동 불가 |
| 사거리 | 6.0~10.0m | 플레이어 체감 시야 정도 |
| 발사 간격 | 2.5~4.0s | |
| 투사체 속도 | 6.0~8.0m/s | 피하기 가능한 수준 |
| 경고 비주얼 지속 | 0.8~1.5s | 회피 타이밍 |
| 점수(EXP) | 20~25 | 처치 시 보상 증가 |

## 5. 이동 패턴

### 5.1 고정형
- 이동 타입: **StationaryMovement** (신규) 또는 `ChaseMovement` 에 `moveSpeed=0`
- 스폰 즉시 현재 위치에 정착. 이후 이동 없음.
- 플레이어가 사거리 안이면 공격 사이클 돌림.
- 사거리 밖이면 대기.

### 5.2 추격형
- 이동 타입: `ChaseMovement` + **거리 유지 로직**
- 거리 > 사거리: 플레이어 향해 접근
- 사거리 도달: 정지 → 공격 사이클
- 공격 종료 후 거리 재평가: 여전히 사거리 안이면 정지 유지, 벗어났으면 추격 재개
- (옵션) 공격 후 백스텝 로직은 추후 검토

## 6. 공격 패턴

### 6.1 투사체 발사 (`attack_projectile`)

```
state: Idle (사거리 밖) → Aiming (사거리 안) → Firing → Cooldown
```
- Aiming 단계: 플레이어 위치 샘플링 (회피 여지 확보)
- Firing: 투사체 1발 생성 후 스폰 위치 → 조준 방향으로 직진
- 투사체: 일정 사거리 후 소멸, 플레이어 충돌 시 데미지

### 6.2 경고 비주얼 (`attack_telegraph`)

```
state: Idle → Warning → Strike → Cooldown
```
- Warning 단계: 타겟 지점에 원/선 이펙트 표시 (지속: 경고 비주얼 지속)
- Strike: 경고 종료 시 해당 지점 데미지 판정 (원형 범위). 플레이어가 범위 밖이면 무피해
- 타겟 지점은 **경고 시작 시점의 플레이어 위치** 고정 (예측 샷)

## 7. 보상

- **경험치:** 20~25 (조합별로 조정)
- **드랍:** 경험치 오브. 엘리트가 아니므로 정수 드랍 없음.

## 8. 데이터 계약 (ScriptableObject)

- **SO 타입:** `EnemyData` (또는 `RangedEnemyData` 세분화 검토)
- **에셋 경로:** `Assets/Data/Enemies/ranged/{variant}.asset`
- **주요 필드:**
  - `behaviorType`: Stationary / Kite
  - `attackType`: Projectile / Telegraph
  - `attackRange`, `attackInterval`
  - `projectilePrefab` (Projectile 공격일 때)
  - `telegraphDuration`, `telegraphRadius` (Telegraph 공격일 때)

## 9. 네트워크

네트워크 기본 규약 [../../systems/network-sync.md](../../systems/network-sync.md).

- 스폰/AI 호스트.
- **투사체:** 각 클라 로컬 렌더. 히트 판정은 호스트.
- **경고 비주얼:** 위치·타이밍을 호스트가 결정, RPC 로 동기화. Strike 판정도 호스트.

## 10. 구현 체크리스트

- [x] `StationaryMovement` 구현 ([Features/Enemy/Adapter/StationaryMovement.cs](../../../Assets/Scripts/Features/Enemy/Adapter/StationaryMovement.cs))
- [x] 추격형 거리 유지 로직 — `KiteMovement(stopDistance = attackRange)` ([Features/Enemy/Adapter/KiteMovement.cs](../../../Assets/Scripts/Features/Enemy/Adapter/KiteMovement.cs))
- [x] 투사체 공격 프리팹 + 풀링 — `EnemyProjectile` + SpawnManager 공용 prefab ([Features/Enemy/Adapter/Attack/EnemyProjectile.cs](../../../Assets/Scripts/Features/Enemy/Adapter/Attack/EnemyProjectile.cs))
- [x] 경고 비주얼 Area 프리팹 + 시간 기반 Strike — `TelegraphZone` (DOTween 금지, 자체 타이머 + GameState 체크) ([Features/Enemy/Adapter/Attack/TelegraphZone.cs](../../../Assets/Scripts/Features/Enemy/Adapter/Attack/TelegraphZone.cs))
- [x] 공격 사이클 — `EnemyAttack` (오케스트레이터, 호스트 권위) + `EnemyTargeter` (타겟 조회) + `EnemyAttackCooldown` (쿨다운) 3 컴포넌트 조합 ([Features/Enemy/Adapter/Attack/](../../../Assets/Scripts/Features/Enemy/Adapter/Attack/))
- [x] 네트워크 RPC (SpawnManager.RaiseEnemyProjectile / RaiseTelegraph)
- [ ] 4가지 조합 SO 생성 (에디터 작업)
- [ ] `photon-sync-auditor`
- [ ] 플레이테스트 (회피 타이밍·밀집 상황)

## 11. 오픈 이슈

- 등장 시점·비율 (밸런싱)
- 고정형이 적 밀집의 장애물로 작용할지, 플레이어가 무시하고 지나갈지 회피 난이도 밸런스
- 공격 방식 추가(빔·관통 등) 우선순위
- 원거리형 자체가 "엘리트화" 될 수 있는지 ([elite.md](elite.md) 와의 경계)
