# 시스템 설계: 퀘스트 (Quest)

> 전투 외 부가 목표를 제공해 플레이 다양성을 확보하는 시스템.

## 1. 메타

| 항목 | 값 |
|---|---|
| 시스템 ID | `quest` |
| 분류 | 게임플레이 / 부가 목표 |
| 의존 레이어 | Adapter (신규 `Features/Quest/`), Shared (`SpawnManager`) |
| 최종 업데이트 | 2026-04-19 |

## 2. 컨셉

맵 위에 흩어진 도전 거점. 모든 플레이어가 거점에 모이면 시작되며, 시작과 동시에 **튼튼한 몹이 구역을 둘러싸 탈출 불가**. 완료 시 **능력치(스탯 부스트) 보상**. 전투의 단조로움을 깨고 팀 협력 / 위치 선정 의사결정을 강요한다.

## 3. 게임 규칙

### 3.1 퀘스트 거점 생성

- **위치:** 맵의 **고정 위치 + 랜덤 위치 혼합** 으로 생성
- 거점은 시각적 마커(예: 빛 기둥)로 표시 — 플레이어가 멀리서 인지 가능

### 3.2 시작 트리거

1. **모든 플레이어**가 거점 일정 반경 내에 진입
2. **일정 시간** 대기 (도중에 누구라도 반경 이탈 시 대기 시간 리셋)
3. 대기 시간 경과 → 퀘스트 시작

### 3.3 격리 메커니즘

- 퀘스트 시작 즉시 **아주 튼튼한 몹** 들이 해당 구역을 **둘러싸고 생성** → 구역을 제한
- 격리 몹은 사실상 처치 불가 수준의 체력 (`enemy_quest_barrier` 안)
- 퀘스트 완료 / 실패 시 격리 몹 자동 제거

### 3.4 퀘스트 종류 (4유형)

| 유형 | 완료 조건 | 실패 조건 |
|---|---|---|
| **목표물 처치** | 지정된 적 N마리 처치 (예: 엘리트 3마리) | 시간 초과 (TBD) |
| **시간 내 킬** | 제한 시간 내 적 N마리 처치 | 시간 초과 |
| **낙하 공격 회피** | N회의 낙하 공격을 **모두 피함** (예: 5회) | **한 명이라도 죽거나 1회라도 맞으면 즉시 실패** |
| **목표물 지키기** | 지정된 NPC/구조물을 일정 시간 보호 | 목표물 파괴 |

### 3.5 보상

- 완료 시 **능력치(스탯 부스트) 선택지** 등장 — [stat-boost.md](stat-boost.md) 의 4등급 체계
- **낮은 등급일수록 등장 확률 높음** (일반 > 희귀 > 영웅 > 전설)
- 획득한 능력치는 **즉시 캐릭터에 적용**

## 4. 수치

> _TBD (밸런싱)_

| 항목 | 값 안 |
|---|---|
| 거점 개수 (1 Run) | TBD (고정 N개 + 랜덤 N개) |
| 거점 진입 반경 | TBD (예: 3m) |
| 시작 대기 시간 | TBD (예: 3초) |
| 격리 몹 체력 | TBD (사실상 무한) |
| 격리 몹 개수 | TBD (구역 둘레 N개) |
| 낙하 공격 회피: 낙하 횟수 | 5회 (확정) |
| 낙하 공격: 경고 → 낙하 시간 | TBD |
| 목표물 처치: 처치 수 | TBD (예: 엘리트 3마리) |
| 목표물 지키기: 보호 시간 | TBD |
| 보상 등급 분포 | TBD ([stat-boost.md § 4](stat-boost.md) 와 동일 안 또는 별도 가중치) |

## 5. 데이터 계약

### 5.1 QuestData (ScriptableObject)

```
Assets/Data/Quests/{quest_id}.asset
QuestData : ScriptableObject
  - questId : string  (예: "quest_kill_elite_01")
  - displayName : string
  - questType : enum { KillTarget, KillInTime, DodgeFalling, Defend }
  - triggerRadius : float
  - waitTime : float
  - timeLimit : float (옵션)
  - targetCount : int (KillTarget / KillInTime / DodgeFalling 회수)
  - barrierEnemyData : EnemyData (격리 몹)
  - rewardRarityWeights : float[4] (Common/Rare/Epic/Legendary 가중치)
```

### 5.2 거점 / 매니저 (Adapter — 신규 Feature)

```
Features/Quest/Adapter/QuestPoint.cs       ← 맵에 배치되는 거점 MonoBehaviour
Features/Quest/Adapter/QuestManager.cs     ← 호스트 권위 진행 관리 (PhotonView)
Features/Quest/Adapter/QuestBarrier.cs     ← 격리 몹 스폰/제거
Features/Quest/Application/QuestService.cs ← 완료/실패 판정 로직 (순수)
Features/Quest/Domain/QuestState.cs        ← Idle / Waiting / Active / Completed / Failed
```

격리 몹은 기존 [Enemy.cs](../../Assets/Scripts/Features/Enemy/Adapter/Enemy.cs) 와 [SpawnManager](../../Assets/Scripts/Shared/Managers/) 를 재사용.

### 5.3 보상 적용

호스트가 완료 판정 후 [stat-boost.md § 5.2](stat-boost.md) 의 RPC 경로 (`RPC_ApplyStatBoost`) 재사용.

## 6. 네트워크

[network-sync.md](../systems/network-sync.md) 규약을 따른다.

- **거점 위치 결정:** 호스트 (Run 시작 시 고정 + 랜덤 RNG)
- **모든 플레이어 진입 판정:** 호스트 (PhotonView 위치 동기화 활용)
- **시작 / 격리 몹 스폰 / 완료 / 실패 판정:** 호스트
- **상태 전파 RPC:**
  - `RPC_QuestStarted(questId)` → UI 알림
  - `RPC_QuestCompleted(questId, rewards[])` → 보상 카드 표시
  - `RPC_QuestFailed(questId, reason)` → 실패 알림 + 격리 몹 제거

## 7. UI / 비주얼

- **거점 마커:** 멀리서도 보이는 빛 기둥 / 미니맵 아이콘
- **진입 시 UI:** "{N}/{팀원수} 진입 — 시작 대기 X초" 진행 바
- **시작 알림:** 화면 한쪽에 퀘스트명 + 목표 표시
- **격리 시각:** 구역 경계가 시각적으로 구분되는 이펙트 (안개/벽)
- **완료/실패 알림:** 결과 + 보상 카드 (3장 선택)

## 8. 관련 문서

- [stat-boost.md](stat-boost.md) — 보상 시스템 (SSOT)
- [enemies/INDEX.md](enemies/INDEX.md) — 격리 몹 안 (`enemy_quest_barrier`)
- [overview.md § 9](overview.md) — 한 줄 요약 + 본 문서 링크
- [network-sync.md](../systems/network-sync.md) — RPC 규약

## 9. 오픈 이슈

- **거점 개수·배치** — 맵 디자인과 함께 결정 (기획)
- **시작 대기 중 한 명 이탈** — 대기 시간 리셋 vs 일시정지 (현재 안: 리셋)
- **퀘스트 도중 플레이어 사망** — 낙하 회피는 즉시 실패, 그 외는? ([rules.md § 3](rules.md) 와 상호작용)
- **격리 몹의 처치 가능성** — 완전 무적 vs 매우 높은 체력 (현재 안: 사실상 무한)
- **퀘스트 거점 재진입** — 한 번 완료한 거점은 사라지는가, 재발생하는가
- **보상 등장 가중치** — [stat-boost.md](stat-boost.md) 의 레벨업 가중치와 동일? 다른가?
- **목표물 지키기의 NPC/구조물** — 별도 데이터 / AI 필요
- **퀘스트 완료 카운트** — Run 통계 / 업적 ([platform-integration.md](../systems/platform-integration.md))
