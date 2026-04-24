# 적 & 보스 인덱스

Sweepin' Dreams 에 등장하는 적과 보스 목록.

> **SSOT:** 수치는 각 적별 SO(`Assets/Data/Enemy/**/*.asset`)와 `Assets/Data/DifficultyData.asset` 의 복제본이다.
> 최종 동기화: 2026-04-24

**템플릿:** [docs/templates/enemy-spec.md](../../templates/enemy-spec.md)
**관련 시스템:** [systems/spawn-rules.md](../../systems/spawn-rules.md), [systems/network-sync.md](../../systems/network-sync.md)

## 설계 원칙

| 구간 | 시간 | 난이도 방향 |
|---|---|---|
| 초반 | 0~3분 | 플레이어가 강화 조합을 구축하는 시간. 적의 위협도 낮음 |
| 중반 | 3~7분 | 강화가 시너지 내기 시작. 밀집도/강도 증가 |
| 후반 | 7~15분 | 빌드 완성. 최대 난이도 |
| 보스전 | **15분** (`GameplayConfig.bossSpawnTime = 900s`) | 팀워크 필요 |

## 일반 적 (프로토타입 4종) — 현재 SO 값

| 적 | 유형 | HP | 이속 | 접촉 데미지 | EXP | 특징 | 상세 |
|---|---|---|---|---|---|---|---|
| 기본 추적형 | Chaser | **50** | **0.6** | **20** | **5** | 단순 추적. 기준선 | [basic.md](basic.md) |
| 빠른형 | Runner | **25** | **0.75** | **15** | **3** | 저체력·고속 | [fast.md](fast.md) |
| 둔한형 | Tank | **150** | **0.3** | **30** | **12** | 경로 차단. 넉백 50% 저항 | [tank.md](tank.md) |
| 무리형 | Swarm | **15** | **0.55** | **10** | **2** | 한 방향 직진, 5~20마리 그룹 | [swarm.md](swarm.md) |

### 등장 비율 (`DifficultyData` 곡선, 시작 → 종료)

| 타입 | 시작 비율 | 종료 비율 |
|---|---|---|
| Chaser | 100% | 30% |
| Runner | 0% | 25% |
| Tank | 0% | 15% |
| Swarm | 0% | 30% |
| Ranged | 1.0 (별도 축) | 1.0 (별도 축) |

**스폰 간격 · 동시 적 수 등 스폰 규칙**은 [systems/spawn-rules.md](../../systems/spawn-rules.md) 가 SSOT.

## 원거리형 / 엘리트 / 보스

| 적 | 유형 | 등장 | 설명 | 상세 |
|---|---|---|---|---|
| 원거리형 | Ranged | 전구간 | 2행동(Turret/Kite) × 2공격(Projectile/Zone) = 4 변형. HP 30 / 공격 20 / 사거리 2 공용 | [ranged.md](ranged.md) |
| 엘리트형 | Elite | 독립 스폰 타이머 | 4종 구현(EliteChaser/Runner/Tank/RangedTurretShot). 정수 100% 드랍 | [elite.md](elite.md) |
| **보스** | Boss | 15분 | 3페이즈. baseHP 20000, 인원 스케일링 ×0.6~1.8 | [boss.md](boss.md) |

## 인원 스케일링 (`DifficultyData.playerScalings`)

| 인원 | 체력 배율 | 동시 적 수 배율 | 경험치 배율 |
|---|---|---|---|
| 1 | 0.6× | 0.6× | 1.0× |
| 2 (기준) | 1.0× | 1.0× | 1.0× |
| 3 | 1.4× | 1.3× | 0.95× |
| 4 | 1.8× | 1.6× | 0.9× |

## 성능 최적화 원칙

- 적 오브젝트 풀링 필수
- 화면 밖 적은 간소화 AI
- NavMesh 대신 단순 추적 (성능 우선)

## 밸런싱 가이드라인

- **플레이 타임 목표:** 보스 등장 15분, 총 17분 내외.
- **레벨 진행:** 보스 등장 시점 혼돈 스킬 3회 선택(레벨 10/20/30 — `GameplayConfig.chaosLevels`).
- **생존:**
  - 초반: 거의 죽지 않음
  - 중반: 이동 실수 시 위험
  - 후반: 밀집도 긴장감. 강한 빌드는 압도 가능
  - 보스: 팀워크 필수. 혼돈 스킬에 따라 난이도 극변
