# 게임 규칙

> **SSOT:** 이 문서의 수치는 `Assets/Data/GameplayConfig.asset`, `Assets/Data/DifficultyData.asset`, 그리고 `Assets/Scripts/Features/Progression/Domain/Formulas/LevelTable.cs` (경험치 공식)의 복제본이다.
> 밸런싱 수정은 **SO/코드에서 먼저** 하고 이 문서는 그 결과를 반영한다.
> 최종 동기화: 2026-04-24

Sweepin' Dreams 의 코어 규칙을 한 곳에 정리. 스킬/적/UI의 "게임이 어떻게 작동하는가"에 직결되지만, 구현 상세(매니저 구조, 씬 구조)는 [systems/managers.md](../systems/managers.md), [systems/scene-structure.md](../systems/scene-structure.md) 참조.

## 1. 스킬 슬롯

- 플레이어당 **최대 6슬롯** (`GameplayConfig.maxSkillSlots = 6`, 액티브 + 패시브 합계).
- **시작 패시브도 슬롯 포함.**
- 슬롯이 모두 찬 상태에서 레벨업:
  - 기존 스킬 레벨업만 선택지에 등장.
  - 모든 스킬이 만렙이면 **능력치(스탯 부스트) 선택지**로 전환.
- **진화** 시 2슬롯이 1슬롯으로 합쳐져 빈 슬롯 생성 → 새 스킬 획득 가능.

## 2. 경험치 & 레벨업

- **팀 공유 경험치.** 적 사망 시 경험치 오브 드롭.
- 플레이어가 **흡수 거리 진입 시 자석처럼 끌려옴** — `GameplayConfig.magnetRange = 0.7`, `magnetSpeed = 2` (패시브로 확장 가능).
- 레벨업은 팀 전체 동시 발생. 모든 플레이어가 동시에 선택 화면(`selectionTimeout = 15초`, `choiceCount = 3`)을 본다.
- **진화 확률:** `evolutionChance = 0.7` (진화 가능 조건 충족 시 선택지에 진화 등장 확률).
- **경험치 곡선 공식** (`LevelTable.GetRequiredExp`):
  ```
  필요 경험치 = 5 + (현재 레벨 × 4)
  ```
  - Lv1→2: 9, Lv5→6: 25, Lv10→11: 45, Lv20→21: 85, Lv30→31: 125.
  - ⚠️ 현재 값은 **테스트용 저감 공식**으로 보임. 출시 밸런싱에서 재조정 대상.

## 3. 사망 & 부활

- 체력 0 → 사망 → **`respawnDelay = 10초`** 부활 타이머.
- 부활: **체력 `respawnHPRatio = 0.5` (50%) 상태로 안전 지점 스폰**.
- 전원 사망 시 **게임 오버** (결과 화면 → 실패).
- 사망/부활 UI는 [flow-design.md § 2.4.4](flow-design.md).

## 4. 인원 스케일링 (`DifficultyData.playerScalings`)

플레이 인원수에 따라 적의 체력/수량/경험치가 자동 조정된다. 기준 2인.

| 인원 | 체력 배율 (`healthMultiplier`) | 동시 적 수 배율 (`maxEnemyMultiplier`) | 경험치 배율 (`expMultiplier`) |
|---|---|---|---|
| 1 (솔로) | **0.6×** | **0.6×** | **1.0×** |
| 2 (기준) | 1.0× | 1.0× | 1.0× |
| 3 | 1.4× | 1.3× | 0.95× |
| 4 | 1.8× | 1.6× | 0.9× |

보스 체력 스케일링은 별도 표(`BossData.hpMultiplier`, [enemies/boss.md § 4](enemies/boss.md)).

스폰 타이밍·난이도 곡선 수식은 [systems/spawn-rules.md](../systems/spawn-rules.md).

## 5. 혼돈 스킬 규칙

- **레벨 10 / 20 / 30** 에서 각 1회씩, 최대 3회 선택 (`GameplayConfig.chaosLevels = [10, 20, 30]`).
- 게임 규칙 자체를 변경하는 스킬. 4등급 체계(일반/희귀/영웅/전설).
- 등급별 등장 가중치: `GameplayConfig.defaultRarityWeights = [60, 25, 12, 3]`.
- **마지막 선택 (레벨 30, 보스 결정):** 각 플레이어가 3개 중 1개 선택 → **선택되지 않은 스킬 중 랜덤 1개가 보스에게 부여**.
- 레벨업 UI에서 등급은 색상으로 구분.

개별 혼돈 스킬은 `Assets/Data/Skill/Chaos/` 에 6종 SO 구현(301~306, 나머지 TBD). 상세 [skills/INDEX.md § 혼돈](skills/INDEX.md), 보스 적용 효과 [enemies/boss.md](enemies/boss.md).

## 6. 호스트 이탈 / 비상 보스전

- 호스트 연결 끊김 감지 → **게임 일시정지, `reconnectWaitTime = 5초` 재연결 대기**.
- 재연결 성공 → 정상 재개.
- 재연결 실패 → **새 호스트 자동 전환 + 비상 보스전 시작** — 보스를 `emergencyBossHPRatio = 0.7` (70% HP)로 즉시 스폰.
- 네트워크 복구 상세는 [systems/network-sync.md](../systems/network-sync.md).

## 7. 플레이어 연결 끊김 (호스트 아님)

- 해당 플레이어 **사망 처리 + 30초 재접속 대기** (구현 측 상수, SO 미반영).
- 재접속 시 안전 지점 부활.
- 실패 시 영구 퇴장 처리.

## 8. 아이템 / 드랍

**일반 적 (`EnemyDropTable.asset`)**
| 항목 | 값 |
|---|---|
| `essenceChance` | 0 (일반 적은 정수 드랍 없음) |
| `weaponChance` | 1.0 |
| `magnetChance` | 0.01 |
| `potionChance` | 0.01 |

**엘리트 (`EliteDropTable.asset`)**
| 항목 | 값 |
|---|---|
| `essenceChance` | 1.0 (100%) |
| `weaponChance` | 0.0001 |
| `magnetChance` | 0 |
| `potionChance` | 0 |

- **경험치 오브 / 자석 / 물약** — 상세는 [items.md](items.md).
- **정수** — 엘리트 드롭. 선착순, 최대 2개. 상세는 [essence.md](essence.md).
- **무기** — 모든 적 드롭. 선착순, 슬롯 4개, 조합 시스템. 상세는 [weapon.md](weapon.md).

## 9. 게임 시간 & 클리어 / 실패 조건

- **보스 등장 시점:** `bossSpawnTime = 900초 (15분)`. 게임 종료 트리거이자 난이도 곡선 정규화 기준(t=1.0).
- **보스 등장 경고:** `bossWarningDuration = 3초`.

| 조건 | 결과 |
|---|---|
| 보스 처치 | 클리어 → 결과 화면 |
| 전원 사망 | 실패 → 결과 화면 |
| "다시 하기" | 대기실 복귀 (방 유지) |
| "나가기" | 타이틀 화면 (방 퇴장) |

## 10. 전투 기본값

| 항목 | 값 (SO) |
|---|---|
| 기본 넉백 강도 | `baseKnockbackForce = 0.9` |
| 투사체 기본 분산각 | `projectileSpreadAngle = 15°` |
| 최대 활성 경험치 오브 | `maxActiveExpOrbs = 200` |
| 투사체 프리워밍 | `projectilePrewarmCount = 30` |
| 경험치 오브 프리워밍 | `expOrbPrewarmCount = 80` |
