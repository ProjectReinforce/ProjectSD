# 적 설계서: 보스 (Boss)

> **SSOT:** 이 문서의 수치는 `Assets/Data/BossData.asset` (SO)의 복제본이다.
> 밸런싱 수정은 **SO에서 먼저** 하고 이 문서는 그 결과를 반영한다.
> 참조 SO: `Assets/Data/BossData.asset`, `Assets/Data/GameplayConfig.asset`
> 최종 동기화: 2026-04-24

## 1. 메타

| 항목 | 값 |
|---|---|
| 엔티티 ID | `boss_main_01` |
| 한국어 이름 (`bossName`) | **드림 이터** |
| 영어 이름 | Dream Eater |
| 분류 | 보스 |
| 등장 시점 | **15분 (`GameplayConfig.bossSpawnTime = 900s`)** |
| 경고 지속 | 3초 (`GameplayConfig.bossWarningDuration`) |
| 최종 업데이트 | 2026-04-24 |

## 2. 컨셉

고정된 3페이즈 패턴 + 매 Run 마다 다른 **혼돈 스킬 1개**가 추가로 적용되어 전투 스타일이 바뀐다. 팀워크 필수.

## 3. 기본 스탯 (`BossData`)

| 필드 | 값 |
|---|---|
| `baseHP` | **20000** |
| `moveSpeed` (Phase 1) | **0.4** |
| `contactDamage` (Phase 1~2) | **30** |
| `knockbackForce` | 0.8 |
| `phase2Threshold` | **0.6** (60% HP에서 Phase 2 전환) |
| `phase3Threshold` | **0.3** (30% HP에서 Phase 3 전환) |

## 4. 인원별 체력 스케일링 (`hpMultiplier`)

| 인원 | 배율 | 실제 HP (`baseHP × multiplier`) |
|---|---|---|
| 1인 | **0.6** | 12,000 |
| 2인 | **1.0** | 20,000 |
| 3인 | **1.4** | 28,000 |
| 4인 | **1.8** | 36,000 |

## 5. 공격 패턴 (3페이즈)

### Phase 1 — HP 100% ~ 60%
| 필드 | 값 |
|---|---|
| 이동 속도 | 0.4 |
| 접촉 데미지 | 30 |
| `p1ShockwaveCooldown` | **5초** |
| `p1ShockwaveDamage` | **40** |
| `p1ShockwaveHalfAngle` | **60°** (전방 부채꼴) |
| `p1ShockwaveRange` | **0.65** |
- 가장 가까운 플레이어 추적 + 전방 부채꼴 충격파.
- **전이 조건:** HP ≤ 60%.

### Phase 2 — HP 60% ~ 30%
| 필드 | 값 |
|---|---|
| `p2MoveSpeed` | **0.48** (+20%) |
| `p2ShockwaveCooldown` | **3초** (단축) |
| `p2CircleZoneCooldown` | **10초** |
| `p2CircleZoneDamage` | **60** |
| `p2CircleZoneDelay` | **3초** (경고 → 폭발) |
| `p2CircleZoneRadius` | **0.4** |
- 충격파 간격 단축 + 랜덤 플레이어 위치에 원형 지대 생성 (3초 후 폭발).
- **전이 조건:** HP ≤ 30%.

### Phase 3 (Enrage) — HP 30% ~ 0%
| 필드 | 값 |
|---|---|
| `p3MoveSpeed` | **0.56** (+40%) |
| `p3ContactDamage` | **50** (+66%) |
| `p3ShockwaveCooldown` | **2초** |
| `p3CircleZoneCount` | **2** (동시 생성) |
| `p3SlowInterval` | **5초** |
| `p3SlowDuration` | **3초** |
| `p3SlowMultiplier` | **0.5** (플레이어 이속 50%) |
- 광폭화: 속도·데미지 상승 + 원형 지대 2개 동시 + 주기적 맵 전체 슬로우.

## 6. 혼돈 스킬별 보스 효과

**레벨 30 선택**의 미선택 혼돈 스킬 중 랜덤 1개가 보스에게 부여된다. [../rules.md § 5](../rules.md) 규칙 참조.

| 혼돈 스킬 | 보스 적용 효과 |
|---|---|
| [유리대포](../skills/chaos/glass-cannon.md) | 보스 체력 50% 감소, 모든 공격 데미지 2배 |
| [연쇄 폭발](../skills/chaos/chain-explosion.md) | 보스가 처치한 플레이어 위치에서 폭발 |
| [폭주 모드](../skills/chaos/berserk.md) | 체력 30% 이하 시 공격 쿨타임 50% 감소, 이동 속도 상승 |
| [가속 엔진](../skills/chaos/acceleration.md) | 보스전 시작 후 매 1분마다 공격력·이동 속도 상승 |
| [단결](../skills/chaos/unity.md) | 플레이어 간 이격 시 보스 데미지 증폭 |
| [도박꾼](../skills/chaos/gambler.md) | 보스 충격파가 3갈래로 분기 |

*나머지 혼돈 스킬의 보스 적용 효과는 TBD (19종 중 6종 Chaos 폴더에 SO 존재).*

**보스 등장 시 UI 표시:** 경고 UI + 혼돈 스킬 아이콘/이름 3초간 ([flow-design.md § 2.4.3](../flow-design.md)).

## 7. 보상

- 보스 처치 = 클리어. 결과 화면으로.
- 드롭: TBD.

## 8. 데이터 계약

- **SO 타입:** `BossData` (`Assets/Scripts/Features/Boss/Adapter/Data/BossData.cs`)
- **에셋 경로:** `Assets/Data/BossData.asset`
- **주요 필드:** 위 스탯 테이블 전체.

## 9. 네트워크

네트워크 기본 규약 [../../systems/network-sync.md](../../systems/network-sync.md).

- **Phase 전이:** 호스트가 판정 → RPC로 전체 클라이언트 동기화
- **충격파/원형 지대:** 호스트가 위치 결정 후 RPC
- **혼돈 스킬 적용:** 보스 스폰 시 1개 결정(호스트) → 모든 클라 동기화
- **비상 보스전:** 호스트 이탈 후 `reconnectWaitTime = 5초` 재연결 실패 시 새 호스트가 `emergencyBossHPRatio = 0.7` 로 스폰 ([rules.md § 6](../rules.md))

## 10. 구현 체크리스트

- [x] `BossData` SO 생성
- [x] Phase별 Movement/Attack 패턴 구현
- [ ] 보스 체력 바 UI (페이즈 구분선 60%/30%)
- [ ] 혼돈 스킬별 보스 효과 구현 (6종 우선, 나머지 TBD)
- [ ] `photon-sync-auditor`
- [ ] 플레이테스트 (1/2/3/4인 각각)

## 11. 오픈 이슈

- 혼돈 스킬 19종 전부의 보스 효과 설계 (현재 6종만 정의)
- Phase 전이 임계치(60%/30%)가 1~4인 전부에 동일한지 검증
- 1인 보스전 난이도 검토 (HP 12,000 vs 단독 DPS)
