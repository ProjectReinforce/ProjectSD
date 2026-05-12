# Meta Unlock — 메타 진행도 / 언락 시스템

영구 진행도(클리어 누적 등) 로 다음 게임에 등장할 컨텐츠를 점진적으로 해금하는 시스템. 본 문서는 **다른 세션이 이 문서만 보고 구현을 끝낼 수 있도록** 작성되었다.

## 1. 메타

| 항목 | 값 |
|---|---|
| 시스템 ID | `meta-unlock` |
| 분류 | 인프라 + 게임플레이 |
| 의존 레이어 | Domain (조건/평가), Adapter (저장/이벤트 후크/UI) |
| 의존 시스템 | [`platform-integration`](platform-integration.md) — `IPlatformService` 위에 얹힘 |
| 최종 업데이트 | 2026-05-12 (구현 완료 — Unit 1/2/3 통과) |
| 구현 상태 | ✅ 구현 완료 (2026-05-12). Phase A Platform 추상화 + Domain/Adapter + 멀티 D5 + 결과 화면 토스트 모두 동작. plan: `~/.claude/plans/synchronous-pondering-taco.md`. ledger: [completed-work.md](../architecture/completed-work.md). |

## 2. 목적

**왜 메타 언락:** vampire-survivors-like 의 리플레이성은 "매 판마다 다른 빌드"에서 나온다. 처음부터 모든 컨텐츠를 풀에 넣으면 ① 신규 유저가 선택 마비를 겪고 ② 조합식 풀이 너무 커져 의도한 빌드를 만들기 어려워진다. **점진 해금**으로 학습 곡선과 빌드 다양성을 동시에 잡는다.

**왜 IPlatformService 위에:** Stove/Steam SDK 가 들어오기 전에도 `LocalPlatformService(PlayerPrefs)` 위에서 즉시 동작. 출시 직전 SDK 갈아끼우면 클라우드 세이브 자동 적용.

**왜 자기 진행도가 자기 게임에 반영:** 호스트 권위로 풀을 결정하지만, 각 플레이어의 진행도는 자기 디바이스 기준이어야 한다. 호스트가 언락 안 한 컨텐츠가 다른 플레이어 화면에 등장 안 하면 "내 진행도가 의미 없다" 는 UX 가 된다.

## 3. 보상 카테고리

| 카테고리 | 단위 | 어디에 영향 |
|---|---|---|
| **스킬** | `SkillData.skillId` | 레벨업 선택지 풀에 추가 |
| **무기 조합식** | `WeaponData.weaponId` (합성 결과물) | `PlayerWeaponInventory` 합성 매칭 |
| **캐릭터** | `CharacterData.id` | 대기실 캐릭터 선택 화면 |
| **새로고침 +N** | RefreshCharge 마일스톤 인덱스 | `LevelUpManager` 초기 충전 |
| **(미래) 코스튬** | `Cosmetic` 슬롯만 예약 | 본 시스템 범위 밖 |

기본 컨텐츠(처음부터 풀에 있는 것) 는 `unlockConditions` 가 비어있고, 신규/특수 컨텐츠만 조건이 부여됨.

## 4. 폴더 구조

```
Assets/Scripts/Features/Unlock/
├── Domain/                              ← UnityEngine / Photon import 금지
│   ├── UnlockCondition.cs               ← [Serializable] struct (polymorphism 대신 enum 분기)
│   ├── UnlockConditionType.cs           ← enum 5종
│   ├── UnlockEvaluator.cs               ← static, type 별 평가
│   ├── UnlockableType.cs                ← enum: Skill/Weapon/Character/RefreshCharge/Cosmetic
│   ├── UnlockableId.cs                  ← VO
│   └── IRunStats.cs                     ← 누적 통계 read-only 뷰
└── Adapter/                             ← Unity / Photon 의존 OK
    ├── Data/
    │   └── UnlockCatalog.cs             ← SO. SO 가 없는 보상(RefreshCharge·Cosmetic) 전용
    ├── LocalRunStatsRecorder.cs         ← 디바이스별 자기 통계 누적
    ├── RunRecordRepository.cs           ← IPlatformService IO
    ├── UnlockTracker.cs                 ← Bootstrap, 평가 + OnNewUnlocks 발화
    └── UnlockSetSync.cs                 ← Photon CustomProperties 기반 자기 셋 공유
```

**WHY `[Serializable] struct`:** 프로젝트에 `[SerializeReference]` 사용 0건. 추상 클래스 + List 직렬화 패턴이 없음. enum 분기가 인스펙터 친화적이고 직렬화 안전.

**WHY Domain/Adapter 분리:** [overview.md § 2](../architecture/overview.md) 의존성 방향 룰. UnlockCondition·UnlockEvaluator 는 순수 C# 으로 단위 테스트 가능해야.

## 5. Domain 정의

```csharp
// Assets/Scripts/Features/Unlock/Domain/UnlockConditionType.cs
namespace SwDreams.Features.Unlock.Domain
{
    public enum UnlockConditionType
    {
        None = 0,
        KillCount,        // targetValue 만큼 누적 킬
        BossDefeat,       // targetIdA = bossId 잡았는지
        RunsCleared,      // targetValue 만큼 클리어
        ZoneVisited,      // targetIdA = zoneId 방문했는지
        DeathByEnemy,     // targetIdA = enemyId 에게 죽은 적 있는지
    }
}
```

```csharp
// Assets/Scripts/Features/Unlock/Domain/UnlockCondition.cs
using System;

namespace SwDreams.Features.Unlock.Domain
{
    [Serializable]
    public struct UnlockCondition
    {
        public UnlockConditionType type;
        public int targetValue;   // KillCount/RunsCleared 의 N
        public int targetIdA;     // 보스/적/존 id
        public int targetIdB;     // 예비
    }
}
```

```csharp
// Assets/Scripts/Features/Unlock/Domain/IRunStats.cs
using System.Collections.Generic;

namespace SwDreams.Features.Unlock.Domain
{
    /// <summary>누적 통계 read-only 뷰. UnlockEvaluator 입력.</summary>
    public interface IRunStats
    {
        int TotalKills { get; }
        int TotalDeaths { get; }
        int TotalRuns { get; }
        int TotalClears { get; }
        IReadOnlyCollection<int> BossDefeatedIds { get; }
        IReadOnlyCollection<int> ZonesVisitedIds { get; }
        IReadOnlyCollection<int> DeathByEnemyIds { get; }
    }
}
```

```csharp
// Assets/Scripts/Features/Unlock/Domain/UnlockEvaluator.cs
namespace SwDreams.Features.Unlock.Domain
{
    public static class UnlockEvaluator
    {
        public static bool Evaluate(UnlockCondition c, IRunStats stats) => c.type switch
        {
            UnlockConditionType.KillCount    => stats.TotalKills    >= c.targetValue,
            UnlockConditionType.RunsCleared  => stats.TotalClears   >= c.targetValue,
            UnlockConditionType.BossDefeat   => stats.BossDefeatedIds.Contains(c.targetIdA),
            UnlockConditionType.ZoneVisited  => stats.ZonesVisitedIds.Contains(c.targetIdA),
            UnlockConditionType.DeathByEnemy => stats.DeathByEnemyIds.Contains(c.targetIdA),
            _ => true,
        };
    }
}
```

## 6. SO 필드 추가 (조건 분산형)

조건은 **각 컨텐츠 SO 옆에** 정의. `UnlockCatalog` 는 SO 가 없는 보상(RefreshCharge·Cosmetic) 전용으로 축소.

| SO | 추가 필드 | 의미 |
|---|---|---|
| `SkillData` | `List<UnlockCondition> unlockConditions`, `bool isHidden` | 비어있으면 처음부터 해금 |
| `WeaponData` | 동일 | **합성 결과물 무기에만 부여**. 기본 무기는 빈 리스트 |
| `CharacterData` | 동일 | 신규 캐릭터에만 부여 |

**WHY 분산형:** 컨텐츠 만들 때 그 자리에서 조건을 세팅. 카탈로그 한 곳에서 관리하는 중앙형보다 "이 스킬에 조건이 있는지" 누락 위험이 낮다.

## 7. UnlockCatalog (SO 없는 보상 전용)

```csharp
// Assets/Scripts/Features/Unlock/Adapter/Data/UnlockCatalog.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using SwDreams.Features.Unlock.Domain;

namespace SwDreams.Features.Unlock.Adapter.Data
{
    [CreateAssetMenu(fileName = "UnlockCatalog", menuName = "SwDreams/Unlock/UnlockCatalog")]
    public class UnlockCatalog : ScriptableObject
    {
        [Serializable]
        public struct RefreshChargeNode
        {
            public UnlockCondition condition;
            public int amount;            // 보통 +1
        }

        [Header("새로고침 마일스톤 (D7)")]
        public List<RefreshChargeNode> refreshChargeNodes = new();

        [Header("(미래) 코스튬")]
        public List<UnlockCondition> cosmeticPlaceholders = new();
    }
}
```

기본 카탈로그 권장 구성: 10/30/50회 클리어 시 각 +1 (총 +3).

## 8. 저장 키 (PlayerPrefs / 클라우드 세이브)

[platform-integration.md § 10](platform-integration.md) 컨벤션 따름.

| 키 | 형식 | 내용 |
|---|---|---|
| `meta.run_stats` | JSON | `IRunStats` 7필드 직렬화 (totalKills/totalDeaths/totalRuns/totalClears/bossDefeatedIds[]/zonesVisitedIds[]/deathByEnemyIds[]) |
| `meta.unlocked_skills` | int[] (JSON) | 언락된 SkillData.skillId 집합 |
| `meta.unlocked_weapons` | int[] (JSON) | 언락된 WeaponData.weaponId (합성 결과물) 집합 |
| `meta.unlocked_characters` | int[] (JSON) | 언락된 CharacterData.id 집합 |
| `meta.unlocked_bonuses` | int[] (JSON) | 달성한 RefreshChargeNode 인덱스 (예: [0,1] = +2) |

## 9. 멀티플레이 권위 모델 (D5 — 핵심)

**원칙: 자기 진행도가 자기 게임에 반영.** 호스트 권위로 풀을 결정하지만, 각 플레이어의 셋을 호스트가 참조해서 그 플레이어용 선택지를 만든다.

| 단계 | 동작 |
|---|---|
| 게임 시작 | `UnlockSetSync` 가 자기 셋을 `PhotonNetwork.LocalPlayer.SetCustomProperties({"unlocked_skills": int[], "unlocked_weapons": int[], "unlocked_characters": int[]})` 로 공유. **타입별 int[] 3개** (Photon SupportedTypes 호환) |
| 스킬 선택지 | `SkillManager.GenerateChoices()` 안에서 `photonView.Owner.ActorNumber` 로 자기 owner 조회 → `UnlockSetSync.IsUnlocked(ownerActor, UnlockableType.Skill, skillId)` 한 줄 필터. SkillManager 인스턴스 자체가 **각 플레이어 자식 오브젝트의 컴포넌트** 라 시그니처 변경 불필요 |
| 무기 합성 | `PlayerWeaponInventory.FindFirstMatchingRecipe()` 도 동일 패턴. 무기 픽업 = 그 플레이어의 인벤토리 → 그 플레이어 셋으로 매칭 가능 여부 판정 |
| 캐릭터 선택 | `CharacterSelectUI.BindButtons()` 가 자기 로컬 셋으로 버튼 활성화. 자기 PC 에서 자기 진행도로 직접 결정 — 네트워크 동기화 불필요 |
| 새로고침 +N | `LevelUpManager` 초기 충전 계산에 `bonusRefreshCharges`(영구 진행도 값) 합산. 본인 진행도이므로 로컬 값만 참조 |

**결과:** A 가 언락한 스킬은 호스트 B 가 언락 안 했어도 A 의 레벨업 선택지에 등장한다.

## 10. 평가 시점 (D11)

**런 종료 후 일괄 체크.** GameClear/GameOver 시 `UnlockTracker` 가 한 번에 평가 → 새로 만족된 보상 리스트를 `OnNewUnlocks(List<UnlockableId>)` 이벤트로 발화 → ResultManager 가 결과 화면 토스트 표시.

**WHY 일괄:**
- D8(결과 화면 토스트) UX 와 자연스러운 일관성 — 한 게임 끝난 직후 "이번 런으로 언락된 것" 이 모인다
- 매 이벤트(킬/존진입)마다 카탈로그 순회 비용 회피
- 새 컨텐츠는 **다음 런부터 등장** — 멀티 동기화 단순화 (런 도중 셋 변경 없음)

## 11. 호출 후크

⚠ **`Enemy.OnDiedWithRef` / `Boss.OnDied` 는 호스트에서만 발화** (검증됨 — Enemy.cs 에 `[PunRPC]` 없음). 따라서 자기 클라가 직접 구독해서 막타 카운트 불가. **사망 RPC 3종이 진입점** — [`run-statistics.md` §4](run-statistics.md) 와 동일 인프라 공유 (B-1a 흐름).

| 위치 | 호출 | 목적 |
|---|---|---|
| `SpawnManager.FlushDeathQueue` 핸들러 (확장됨, RpcTarget.All) — 자기 클라 | `killerActor == self` → `runStats.OnKill(enemyId)` | Enemy 자기 막타 킬 (페이로드에 `killerActor` 이미 있음, `killerSkillId` 신규 추가) |
| **신규 `RPC_BossDied(int bossId)`** (RpcTarget.All) — 모든 클라 | **무조건** → `runStats.OnBossDefeat(bossId)` | 보스 처치 = 모든 파티원 카운트 (D13, 가해자 매칭 안 함) |
| `PlayerHealth.RPC_TakeDamage` 핸들러 (확장됨, RpcTarget.All) — 자기 사망 | 자기 viewId 매칭 시 `LastDamagerEnemyId` 기록 → `OnDied` 시 `runStats.OnDeath(lastDamagerEnemyId)` | 자기 사망 + 가해자 추적 (페이로드 `attackerEnemyId` 신규 추가, `PlayerHealth.LastDamagerEnemyId` 필드 신규) |
| `QuestZone`/`AreaZone` OnTriggerEnter (자기 캐릭터) | `runStats.OnZoneVisited(zoneId)` | 위치 도달 |
| `GameManager.OnStateChanged(GameClear/GameOver)` (모든 클라 동기화) | `UnlockTracker.OnRunEnded(runStats, isCleared)` | 런 종료 일괄 평가 |

**WHY 사망 RPC 진입점 분리 (B-1a):** ① 매 데미지마다 통합 RPC 발화는 90마리 동시 + 다타격 스킬 환경에서 트래픽 부담. ② 사망 시점만 RPC 페이로드 확장 → 트래픽 최소. ③ 메타 진행도와 인-런 통계가 같은 사망 RPC 진입점을 공유하므로 카운트 일관성 보장.

**WHY 보스만 가해자 매칭 안 함 (D13):** 협동 보스전 — 막타 가해자 가릴 필요 없음. 모든 파티원이 처치 카운트 공유. RPC 단순화 + 검증 부담 ↓.

## 12. AchievementId 와의 매핑

[platform-integration.md § 5](platform-integration.md) 의 `Stat_*` 그대로 재사용.

| 시점 | 호출 |
|---|---|
| 런 종료 | `IPlatformService.IncrementStat(Stat_TotalKills, runKills)` |
| 런 종료 | `IncrementStat(Stat_TotalDeaths, runDeaths)` |
| 런 종료 | `IncrementStat(Stat_TotalRuns, 1)` |
| 클리어 시 | `IncrementStat(Stat_TotalClears, 1)` + `UnlockAchievement(FirstClear)` 외 |

PlayerPrefs 의 `meta.run_stats` 는 백업/조건 평가용으로 유지. SDK 자체 stat API 와 이중화.

## 13. 검증

### 단일 케이스
- [ ] 스킬 언락 — `SkillData` 1개에 `KillCountCondition(target=10)` 부여 → 1런 진행 → 다음 게임 풀 등장.
- [ ] 무기 조합 언락 — 합성 결과 `WeaponData` 1개에 `BossDefeatCondition` 부여 → 보스 처치 전엔 합성 안 되고, 처치 후 자동 합성 발동.
- [ ] 캐릭터 언락 — `CharacterData` 1개에 `RunsClearedCondition(target=3)` → 3회 클리어 후 선택 가능.
- [ ] 새로고침 +N — `bonusRefreshCharges` 강제 set 후 LevelUp 패널 잔여 횟수 단계별 +1/+2/+3.
- [ ] 토스트 — 마지막 1킬로 KillCountCondition 충족 후 클리어 → 결과 화면 끝부분 "신규 언락" 리스트 노출.

### 멀티플레이 (D5 핵심)
- [ ] 2클라가 동일 일반 적을 같이 잡았을 때, 마지막 가해자 본인 PC 에만 킬 카운트.
- [ ] **보스 처치 시 모든 파티원의 BossDefeatedIds 셋에 추가** (D13 — 가해자 매칭 안 함). 클라이언트만 보스 막타 시에도 호스트 포함 모든 파티원에게 카운트.
- [ ] **A 가 언락한 스킬이 호스트 B 의 진행도와 무관하게 A 의 레벨업 선택지에 등장.**
- [ ] **A 가 언락한 무기 조합식이 A 가 픽업해 합성 시 발동.**
- [ ] 캐릭터 선택 시 각 클라가 자기 진행도로 표시.

### 인프라
- [ ] `Features/Unlock/Domain/` 의 모든 .cs 가 `using UnityEngine;` / `using Photon.*;` 없음 (architecture-guardian).
- [ ] `[SerializeReference]` 도입하지 않음.
- [ ] `LocalPlatformService` PlayerPrefs 백엔드 — 종료/재시작 후 Load 정상.
- [ ] 향후 `StovePlatformService` 로 갈아끼워도 동일 동작.

## 14. 비범위

- 스킨/코스튬 시스템 자체 (UnlockableType.Cosmetic 슬롯만 호환).
- 메타 XP (누적 경험치 → 단계 진행도) — 후순위 별도 시스템.
- "특정 스킬로 처치" 조건 — D1 에서 제외. **단 [`run-statistics.md`](run-statistics.md) 의 sourceSkillId 인프라 도입 시 부활 비용 거의 0** → 구현 단계 재검토.
- 시작 골드 / 스타팅 패시브 / 시작 슬롯+1 — 밸런스 위험.

## 15. 결정사항 (사용자 확정)

| ID | 결정 | 이유 |
|---|---|---|
| D1 | "특정 스킬로 처치" 조건 제외 | 비용 절감. 인프라 깔리면 재검토 |
| D2 | MVP 보상 = 스킬 + 무기 조합식 + 캐릭터 + 새로고침 | 스킨은 별도 진행, 시작 슬롯/패시브는 밸런스 위험 |
| D3 | 메타 XP 후순위 분리 | MVP 는 조건식만 |
| D4 | 진행도 누적 = 디바이스별 개별 | 표준 메타 진행도 UX |
| D5 | 진행도 사용 = 자기 진행도가 자기 게임에 반영 | 호스트 권위 시 "내 진행이 의미 없는" UX 회피 |
| D6 | 조건 정의 = 각 SO 분산형 | 컨텐츠 옆에 조건, 누락 위험 ↓ |
| D7 | 새로고침 +N = 마일스톤 단계 (10/30/50회) | 육감 단위 진척감 |
| D8 | 알림 UX = 결과 화면 토스트 | 런 종료 직후 보상감 |
| D9 | 저장 단위 = PC 1대 = 1 진행도 | MVP 단순화. SDK 도입 시 클라우드로 격리 |
| D11 | 평가 시점 = 런 종료 후 일괄 | 토스트 UX 일관, 비용 ↓ |
| D13 | 보스 처치 = 모든 파티원 카운트 (가해자 매칭 안 함) | 협동 보스전 — 막타 가해자 가릴 필요 없음. RPC 단순화 (신규 `RPC_BossDied(bossId)` RpcTarget.All 1회 발화) + 검증 부담 ↓ |

## 16. 외부 참조

- [`platform-integration.md`](platform-integration.md) — IPlatformService 시그니처 / AchievementId / 저장 키 컨벤션
- [`run-statistics.md`](run-statistics.md) — 인-런 통계 (결과 화면용, 별도 축)
- [overview.md § 2](../architecture/overview.md) — 의존성 방향 / Domain 순수성

## 17. 알려진 제약

- [ ] 호스트 마이그레이션 시 새 호스트가 모든 플레이어의 unlockedSet 을 다시 읽어야 함 — Photon CustomProperties 기반이라 자동 보장 (검증 필요)
- [ ] 클라이언트가 게임 도중 언락된 컨텐츠는 그 런에 적용 안 됨 (D11 일괄 평가 정책). 다음 런부터 등장
- [ ] 같은 PC 에서 여러 게임 계정 사용 시 진행도 섞임 (D9 단일 유저 가정) — Stove/Steam SDK 도입 시 클라우드로 자동 격리

## 18. 호스트 마이그레이션 영향 분석

[`HostMigrationHandler`](../../Assets/Scripts/Shared/Managers/HostMigrationHandler.cs) 가 5초 대기 후 적/보스 정리 + GameTime 기준 자연 재개를 제공.

| 메타 진행도 컴포넌트 | 마이그레이션 영향 |
|---|---|
| `LocalRunStatsRecorder` (자기 클라 누적) | ✅ 무관. 자기 PC 에 데이터 보존 |
| `Enemy.OnDiedWithRef` 자기 막타 카운트 | ✅ 무관. 마이그레이션 전 막타는 이미 누적됨. 마이그레이션 후엔 적이 정리되므로 카운트 일시 중단 → 재개 |
| `Boss.OnDied` 보스 처치 | ✅ 게임이 계속되어 새 호스트로 보스 재스폰 → 처치 시 정상 카운트. 단 마이그레이션 직전에 보스가 죽기 직전이었어도 정리되어 다시 시작해야 함 |
| `GameClear`/`GameOver` 발화 | ✅ 게임이 정상 종료되면 카운트, 비정상이면 카운트 X (정상 동작) |
| `unlockedSet` Photon CustomProperties | ✅ Photon 자체가 `Player.CustomProperties` 를 마이그레이션 후에도 보존 |
| `UnlockTracker` 영구 저장 | ✅ PlayerPrefs 기반. 자기 PC, 호스트 무관 |

**유일한 사용자 인지 영향:** 보스 직전 호스트 이탈 시 보스가 다시 스폰되어 한 번 더 잡아야 함. 메타 진행도는 어차피 클리어 시점에 카운트되므로 결국 정상 카운트됨.

**인-런 통계는 [`run-statistics.md`](run-statistics.md) §3, §6 의 분산 추적 설계로 마이그레이션 무관하게 보존됨.**
