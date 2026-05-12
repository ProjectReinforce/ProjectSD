# Run Statistics — 인-런 통계 / 결과 화면

한 게임 동안 플레이어별·스킬별로 누적된 통계를 결과 화면에 시각화하는 시스템. 메타 진행도와는 **별도 축** (휘발성, 호스트 권위).

## 1. 메타

| 항목 | 값 |
|---|---|
| 시스템 ID | `run-statistics` |
| 분류 | UI / 게임플레이 |
| 의존 레이어 | Adapter (Photon RPC, MonoBehaviour) |
| 의존 시스템 | [`meta-unlock`](meta-unlock.md) — sourceSkillId 인프라 공유, 일부 통계는 영구 누적에도 사용 |
| 최종 업데이트 | 2026-05-07 (§ 7 결과 화면 카드 디자인 — MVP + 보조 표창 + 빌드 후크 약 + 솔로 케이스) |
| 구현 상태 | 🟡 진행 중 — plan: `~/.claude/plans/synchronous-pondering-taco.md` |

## 2. 목적

**왜:** 결과 화면에서 "누구의 빌드가 가장 효율적이었나" 를 시각화. 멀티 협동 플레이의 핵심 만족감 = 자기 빌드 평가 + 동료와 비교. 현재는 팀 합산 킬/사망만 보여주고 있어 정보가 부족.

**왜 sourceSkillId 인프라:** "이번 런에서 어떤 스킬이 가장 강했나" 를 보여주려면 데미지 컨텍스트에 발사 스킬 ID 가 실려야 함. 본 인프라는 [`meta-unlock`](meta-unlock.md) 의 D1 ("특정 스킬로 처치" 조건) 도 사실상 거의 무비용으로 가능하게 한다.

**왜 분산 추적 (B-1a):** 호스트 마이그레이션 시 호스트 단일 dict 손실 위험 회피. 가해 데미지는 자기 발사 시점에 자기 PC 누적(호스트 적용 결과 무관) — 작은 오차(< 1%) 감수하고 트래픽 안전 + 마이그레이션 안전. 자기 막타 킬은 사망 RPC 페이로드 확장으로 모든 클라 매칭. 자세한 흐름은 §4 참조.

## 3. 데이터 구조

### 분산 추적 — 각 클라이언트 보유 (휘발성)

**WHY 분산 추적:** 호스트 마이그레이션([HostMigrationHandler.cs](../../Assets/Scripts/Shared/Managers/HostMigrationHandler.cs)) 시 호스트가 보유한 단일 dict 가 손실되는 것을 회피. 데미지 적용 RPC 가 이미 모든 클라에 전파되므로(`network-sync.md` §3 — 호스트 데미지 판정 + 클라 렌더링), 각 클라가 자기 ActorNumber 매칭 데미지를 자기 PC 에 누적.

```csharp
// 각 클라이언트가 자기 PC 에 보유. 결과 시점 SendLocalBuildToHost 에 같이 보냄.

class LocalRunStats   // 자기 통계만
{
    public int kills;           // 자기 막타 카운트
    public int deaths;          // 자기 사망 횟수
    public float damageDealt;   // 자기가 가한 누적 데미지
    public float damageTaken;   // 자기가 받은 누적 데미지
    public Dictionary<int /*skillId*/, SkillRunStats> bySkill;
}

class SkillRunStats
{
    public int fireCount;       // 발동 횟수
    public int killCount;       // 그 스킬로 막타 친 적 수
    public float damageDealt;   // 그 스킬 누적 데미지
}
```

### 호스트는 합산만
호스트가 권위적으로 보유하는 별도 dict 없음. 결과 시점에 각 클라가 보낸 `LocalRunStats` 를 그대로 `PlayerBuildData` 에 합쳐 브로드캐스트.

### 보스 처치 정책 (D13)
**보스는 모든 파티원이 처치 카운트** (가해자 매칭 안 함). 보스 가한 데미지 통계는 자기 발사분 자기 누적 (일반 적과 동일). 결과 화면에는 모든 플레이어 카드에 "보스 처치 ✓" 동일 표시.

### RPC 직렬화 (`PlayerBuildData` 확장)

기존 `Assets/Scripts/Shared/Domain/GameResult.cs::PlayerBuildData` 에 다음 필드 추가:

```csharp
public class PlayerBuildData
{
    // ... 기존 필드: ActorNumber, PlayerName, CharacterId, SkillIds, SkillLevels, ChaosTypeIds

    // 인-런 통계 (D10)
    public int RunKills;
    public int RunDeaths;
    public float DamageDealt;
    public float DamageTaken;

    public int[] SkillFireCounts;     // SkillIds 와 같은 인덱스
    public int[] SkillKillCounts;
    public float[] SkillDamageDealt;
}
```

`ResultManager.BroadcastResult` 의 기존 int[] flatten 패턴 그대로 확장. float 은 `BitConverter.SingleToInt32Bits` 로 int 캐스팅 후 packing — Photon 기본 타입 호환.

## 4. sourceSkillId 인프라 (B-1a 흐름)

> ⚠ **2026-05-06 정정:** [NetworkAdapter.cs:14-28](../../Assets/Scripts/Shared/Network/NetworkAdapter.cs) 의 `RPC_NotifyDamageApplied` 는 Debug.Log 만 찍는 Stub 으로 호출자 없음. 매 데미지마다 통합 RPC 발화는 90마리 동시 + 다타격 스킬 환경에서 트래픽 부담 → **B-1a (자기 발사 시점 자기 PC 누적 + 사망 RPC 페이로드 확장)** 흐름으로 변경.

### 데미지 컨텍스트 확장

```csharp
// SpawnContext / TriggerContext (DealDamageHandler / DealDamageNearbyHandler 등이 사용)
public struct DamageContext
{
    public int attackerActorNumber;
    public int sourceSkillId;       // ← 신규 (skillData.skillId)
    public float damage;
    // ... 기존 필드
}
```

### 발사 경로 (sourceSkillId 전달)

스킬 Executor / Spawner 가 자기 SkillData.skillId 를 컨텍스트에 실어보낸다. 모든 데미지 발사 경로(Projectile / Area / Orbital / Placed / Debuff) 가 일관되게 적용해야 함.

### 가해 데미지 누적 (B-1a — 자기 발사 시점 자기 PC)

**원칙:** 클라가 자기 발사한 스킬이 적과 충돌해 데미지가 산정되는 시점 (DealDamage*Handler 진입 직후) 에 자기 PC 의 `LocalStatsRecorder` 에 누적. 호스트 적용 결과를 기다리지 않음.

```csharp
// DealDamageHandler.Execute() 내부 — Enemy.TakeDamage() 호출 직전
if (context.attackerActorNumber == PhotonNetwork.LocalPlayer.ActorNumber)
{
    LocalStatsRecorder.Instance?.OnFire(context.sourceSkillId);  // 1회 발사당
    LocalStatsRecorder.Instance?.AddDamage(context.sourceSkillId, finalDamage);
}
enemy.LastDamagerActorNumber = context.attackerActorNumber;
enemy.LastDamagerSkillId     = context.sourceSkillId;
enemy.TakeDamage(finalDamage, isCrit);
```

**작은 오차 감수:** 호스트가 reject 한 데미지(예: 무적/감면)도 자기 가해분에 카운트. 일반적으로 < 1% 수준이라 결과 화면 시각화 정확성에 영향 미미.

**WHY:** 호스트 마이그레이션 시 자기 PC 데이터 보존. 매 데미지 RPC 발화로 인한 트래픽 회피.

### 자기 막타 킬 누적 (사망 RPC 페이로드 확장)

| 사망 주체 | 진입점 RPC | 페이로드 확장 | 누적 로직 |
|---|---|---|---|
| 일반 적 | [SpawnManager.cs:1046 `FlushDeathQueue`](../../Assets/Scripts/Shared/Managers/SpawnManager.cs) (이미 batched 사망 RPC) | `(enemyId, posX, posY, exp, killerActor, killerSkillId)` — `killerSkillId` 신규 | `killerActor == self` → `OnKill(killerSkillId, enemyId)` |
| 보스 | **신규 `RPC_BossDied(int bossId)`** (RpcTarget.All) — `Boss.Die()` 직전 1회 발화 | 가해자 매칭 안 함 | **모든 파티원 무조건** `OnBossDefeat(bossId)` 자기 PC 카운트 (D13) |
| 자기 사망 | [PlayerHealth.cs:144 `RPC_TakeDamage`](../../Assets/Scripts/Features/Character/Adapter/PlayerHealth.cs) (RpcTarget.All) | `+attackerEnemyId` 신규 | 자기 클라 수신 시 `LastDamagerEnemyId` 기록 → `OnDied` 시 `OnDeath(enemyId)` |

**WHY 보스만 가해자 매칭 안 함 (D13):** 협동 보스전 — 막타 가해자 가릴 필요 없음. 모든 파티원이 처치 카운트 공유. RPC 단순화 + 검증 부담 ↓.

### 자기 받은 데미지 누적

`PlayerHealth.RPC_TakeDamage` 가 이미 `RpcTarget.All` 로 모든 클라에 발화됨. 자기 PC 가 자기 viewId 매칭 시 `LocalStatsRecorder.AddDamageTaken(damage)` 누적. 별도 RPC 불필요.

### 호스트 마이그레이션 안전성

| 시점 | 가해 데미지 | 자기 막타 킬 | 자기 사망 |
|---|---|---|---|
| 마이그레이션 전 | 자기 PC 누적 ✓ | 사망 RPC `RpcTarget.All` 발화 완료 → 자기 PC 카운트 ✓ | `RPC_TakeDamage` 페이로드 자기 PC 기록 ✓ |
| 마이그레이션 도중 (5초) | 발사 RPC 호스트 부재 — 자연 일시정지 | [HostMigrationHandler](../../Assets/Scripts/Shared/Managers/HostMigrationHandler.cs) 가 적/보스 정리. 카운트 일시 중단 | i-frame/respawn 일시 중단. 사망 자체 보존 |
| 마이그레이션 후 | 새 호스트 흐름으로 정상 누적 | 새 적/보스 스폰 → 정상 RPC → 정상 카운트 | 새 호스트가 처리 이어받음 |

**유일한 손실 케이스:** 마이그레이션 도중 보스가 죽기 직전이었으면 보스가 정리되어 다시 스폰 → 재처치 시 정상 카운트 (단지 한 번 더 잡아야 함). 이미 [`meta-unlock.md` §18](meta-unlock.md) 에 문서화됨.

## 5. LocalStatsRecorder (신규)

각 클라이언트에 1개씩. 자기 통계만 누적.

```csharp
public class LocalStatsRecorder : MonoBehaviour
{
    public static LocalStatsRecorder Instance { get; private set; }

    private LocalRunStats stats = new();

    public void OnFire(int sourceSkillId) { /* ... */ }
    public void AddDamage(int sourceSkillId, float dmg) { stats.damageDealt += dmg; /* skill bucket 누적 */ }
    public void AddDamageTaken(float dmg) { stats.damageTaken += dmg; }
    public void OnKill(int sourceSkillId) { stats.kills++; /* skill bucket killCount++ */ }
    public void OnDeath() { stats.deaths++; }

    public LocalRunStats Snapshot() => stats;  // SendLocalBuildToHost 시 호출
}
```

**기존 `GameStatTracker` 는 그대로 유지** (호스트 권위 단순 카운터, 결과 화면 팀 합계용). 새 분산 통계는 별도 인프라.

## 6. ResultManager 확장 — 각 클라가 자기 통계 같이 전송

```csharp
// SendLocalBuildToHost 확장
var localStats = LocalStatsRecorder.Instance?.Snapshot();

// 기존 RPC 인자 + 통계 필드 추가
photonView.RPC(nameof(RPC_SendBuildToHost), RpcTarget.MasterClient,
    PhotonNetwork.LocalPlayer.ActorNumber,
    playerName, characterId, skillIds, skillLevels, chaosIds,
    // 신규
    localStats?.kills ?? 0,
    localStats?.deaths ?? 0,
    localStats?.damageDealt ?? 0f,
    localStats?.damageTaken ?? 0f,
    SerializeSkillStats(localStats, skillIds)  // SkillIds 순서대로 fire/kill/damage flatten
);
```

`RPC_SendBuildToHost` 는 받은 통계를 `PlayerBuildData` 에 그대로 채움. `BroadcastResult` 는 추가 합산 없이 그대로 RPC 로 모든 클라에 전송. **호스트는 단순 합산만** — 어떤 데이터도 호스트에 의존하지 않음.

### 호스트 마이그레이션 시 동작
- 데미지 적용은 새 호스트가 이어받아 RPC 발화
- 각 클라의 `LocalStatsRecorder` 는 자기 PC 에 그대로 보존
- 결과 시점 = 새 호스트가 마이그레이션 후의 데이터까지 합쳐 브로드캐스트
- **마이그레이션 전 통계가 손실되지 않음** ✓

## 7. UI 확장 (`ResultPanelUI` 또는 `UIManager.ShowResult`)

### 7.1 디자인 의도 (2026-05-07 결정)

**핵심 가치 — "다음 런에 영향":** 결과 화면이 단순 회고가 아니라 **다음 게임의 빌드 영감 진입점** 이어야 한다. 따라서:
- 누가 MVP 인지 시각적으로 명확
- MVP 가 어떤 빌드를 썼는지 한눈에 보임
- 다른 플레이어도 "내 영역에서 1위" 표창으로 결과창 만족도 확보 (4명 모두 한 마디씩)

### 7.2 카드 그리드 (Risk of Rain 2 / Deep Rock Galactic 식)

**채택 안 = A안 + 보조 표창 (C안 절충):**

```
┌──────── 클리어! / 실패  플레이타임 14:32  팀 처치 1,243 ────────┐
│                                                                  │
│  ┌────────┐  ┌────────┐  ┌────────┐  ┌────────┐                │
│  │ MVP 🏆 │  │  최다  │  │  최소  │  │  탱킹  │                │
│  │ [초상화]│  │  처치  │  │  사망  │  │  MVP  │                │
│  │ 닉네임  │  │ 닉네임 │  │ 닉네임 │  │ 닉네임│                │
│  │ 12.3k  │  │ 9.2k  │  │ 6.5k  │  │ 4.1k │                  │
│  │ 킬 401  │  │ 312   │  │ 287   │  │ 198  │                  │
│  │ [스킬 6]│  │ [6]   │  │ [6]   │  │ [6]  │                  │
│  └────────┘  └────────┘  └────────┘  └────────┘                │
│                                                                  │
│              [다시 하기]    [나가기]                              │
└──────────────────────────────────────────────────────────────────┘
```

**MVP 결정:** 가해 데미지 1위 1명만. 황금 테두리 + 트로피 아이콘.
**보조 표창:** 나머지 3명에게 가장 두드러진 항목 1개씩 작은 배지로 표시 (예: "최다 처치", "최소 사망", "탱킹 MVP", "최다 부활/협력" 등 보유 통계 안에서 선택). 1명이 2~3개 항목 1위면 MVP 후 다음 1위에게 양보 — 모든 카드에 배지 1개씩 보이도록 분배.

**카드 구성요소:**
- 캐릭터 초상화 (없으면 색 아바타로 fallback)
- 닉네임 + 캐릭터 이름
- 큰 숫자 1개: 가해 데미지 (DPS = damageDealt/playTime 보조 표시)
- 작은 숫자 4개: 킬 / 사망 / 받은 데미지 / 보스 처치(D13 모든 카드 동일)
- 스킬 아이콘 6슬롯 (level 표시) — 정수/무기/혼돈 별도 행
- **메인 스킬 1개 강조** (가장 큰 데미지 낸 스킬 = 빌드의 정체성. 빌드 후크 = 약, 자동 명명 X)

### 7.3 카드 상세 패널 (호버/클릭)

카드 호버/클릭 시 우측 또는 카드 위에 상세 패널 슬라이드 인:
- 스킬별 누적 데미지 막대 차트 (가로 바, 데미지 비중 기준 그라데이션)
- 정수/무기/혼돈 아이콘 + 이름 (R15-A SkillDB 접근자 도입 후 가능)
- 받은 데미지 / 막타 / 보조 통계

### 7.4 솔로 (PlayerCount == 1)

별도 화면 분기 없이 **같은 컴포넌트로 카드 1장만 크게**. MVP/표창 라벨 미표시. 솔로 전용 시간축 DPS 그래프 등은 향후 별건.

### 7.5 빌드 후크 강도 = 약

- 정보 노출만 (메인 스킬 강조 + 정수/무기/혼돈 표시)
- ❌ 자동 빌드 명명 ("검무 + 폭주 모드 빌드" 등 — 진화/혼돈 조합 룰 테이블 필요, 작업 비용 큼)
- ❌ "이 빌드 다시 하기" 버튼 — `BuildPreset` 영속 저장 시스템 필요, Save/Load 도입 후 별건

### 7.6 팀 합계 패널 (하단)

- 총 킬 · **총 데미지** · 생존 시간

### 7.7 선결 조건

[implementation-roadmap.md § R15-A](../architecture/implementation-roadmap.md) **SkillDatabase 글로벌 접근자** 가 본 디자인의 선결. 스킬 아이콘/이름이 없으면 카드의 빌드 정체성 강조가 의미 없음. R15-A 5분 → R15-B(본 § 7.2~7.6 구현) 순서.

## 8. 멀티플레이 권위 모델

| 동작 | 권위 |
|---|---|
| 데미지 적용 (적/플레이어 체력 감소) | 호스트 |
| 데미지 RPC 전파 | 호스트 → 모든 클라 (네트워크 sync §3) |
| **통계 누적** | **각 클라가 자기 ActorNumber 매칭만** (분산) |
| 결과 시점 통계 → 호스트 합산 | 각 클라가 `RPC_SendBuildToHost` 에 자기 통계 첨부 |
| 결과 RPC 전파 | 호스트 `BroadcastResult` 로 한 번 전체 전송 |

**WHY 분산:** 호스트 마이그레이션 시 단일 권위 dict 손실 회피. 데미지 적용 RPC 가 모든 클라에 한 번씩 발화되므로 ActorNumber 매칭 필터로 한 번만 카운트 — 이중 카운트 위험 없음. 클라 예측 데미지는 호스트 확정 RPC 가 아니므로 통계에 반영되지 않음 (자동 보장).

## 9. 메타 진행도와의 통합

`LocalRunStatsRecorder` ([`meta-unlock`](meta-unlock.md) §11) 는 자기 ActorNumber 의 `PlayerRunStats` 만 추출해 영구 누적에 합산. 단:

- **메타 진행도 영구 저장 = 단순 카운터만** (totalKills/totalDeaths/totalRuns/totalClears + boss/zone/death-by 셋)
- 스킬별 통계는 결과 화면 표시 후 폐기. 영구화 X
- "스킬별 누적 데미지" 가 영구 진척감으로 의미 있다고 판단되면 향후 별도 시스템 (예: 메타 XP)

## 10. 검증

- [ ] 2클라가 동일 일반 적을 같이 잡았을 때 막타 가해자 본인의 KillCount 만 +1.
- [ ] **보스 처치 시 모든 파티원의 BossDefeat 카운트가 +1** (가해자 매칭 안 함, D13).
- [ ] 데미지는 자기 발사분 자기 누적 — 두 플레이어의 DamageDealt 합 ≈ 적 처치 시점까지 입은 데미지 총합 (호스트 reject 데미지 < 1% 오차 허용).
- [ ] 스킬별 차트가 각 플레이어 카드에 표시되고 합계가 `PlayerBuildData.DamageDealt` 와 ±1% 이내 일치.
- [ ] 팀 합계 패널의 총 데미지 = Σ 각 플레이어 DamageDealt.
- [ ] sourceSkillId 가 클라이언트 예측 데미지가 아닌 자기 발사 경로에서만 카운트 (이중 카운트 X).
- [ ] **호스트 마이그레이션 시 통계 보존** — 보스 직전 호스트가 나가서 마이그레이션 → 보스 처치 후 결과 화면에 마이그레이션 전 데미지/킬도 정상 표시.
- [ ] 사망 RPC 가 모든 클라에 발화될 때 ActorNumber 매칭 필터로 한 번만 카운트 (보스 제외 — 보스는 모든 클라 카운트).

## 11. 비범위

- 통계 영구 저장 (스킬별 누적 데미지 등) — 본 시스템은 한 런 휘발성. 영구화는 [`meta-unlock`](meta-unlock.md) 의 단순 카운터만.
- 리더보드 / 친구 비교 — Phase B/C SDK 도입 시 별도 검토.
- 스킬 밸런스 자동 조정 — 통계는 표시만, 게임 로직 영향 X.
- 마이그레이션 도중에 떠나간 플레이어의 통계 — 결과 화면에 그 플레이어 슬롯이 표시되지 않으므로 자연스럽게 무시.

## 12. 외부 참조

- [`meta-unlock.md`](meta-unlock.md) — D10 결정 / sourceSkillId 인프라 공유
- [`platform-integration.md`](platform-integration.md) §7 — `ResultManager.BroadcastResult` 후크 위치
- [overview.md § 2](../architecture/overview.md) — Adapter 레이어 규칙
- 기존 코드:
  - `Assets/Scripts/Shared/Managers/GameStatTracker.cs` — 단일 카운터 → ActorNumber dict 확장 대상
  - `Assets/Scripts/Shared/Managers/ResultManager.cs::BroadcastResult` — 직렬화 확장 대상
  - `Assets/Scripts/Shared/Domain/GameResult.cs::PlayerBuildData` — 필드 추가 대상

## 13. 알려진 제약

- [ ] 클라이언트가 결과 화면 전에 떠나면 그 플레이어의 통계는 결과 RPC 도착 안 함 → 결과 화면에 슬롯 없음 (HostMigrationHandler.HandlePlayerDisconnect 가 비활성 처리)
- [ ] 데미지 컨텍스트에 sourceSkillId 누락된 경로는 "Unknown" 으로 카운트되어 차트에 표시 안 됨 — 모든 데미지 경로 보강 필요 (구현 직전 grep 으로 누락 경로 확인)
- [ ] 보스 페이즈 전환 / 보스 사망 페이드아웃 동안의 데미지 — 호스트 데미지 RPC 가 끊기는 시점이 있다면 누락. photon-sync-auditor 로 검사
- [x] **B-1a 흐름 채택 (2026-05-06).** [NetworkAdapter.cs:14-28](../../Assets/Scripts/Shared/Network/NetworkAdapter.cs) 의 `RPC_NotifyDamageApplied` 는 Stub (호출자 없음). 매 데미지마다 통합 RPC 발화는 90마리 동시 + 다타격 스킬 환경에서 트래픽 부담 → 가해 데미지는 자기 발사 시점 자기 PC 누적, 자기 막타 킬은 사망 RPC 페이로드 확장(`SpawnManager.FlushDeathQueue` + 신규 `RPC_BossDied` + `PlayerHealth.RPC_TakeDamage`) 으로 카운트.
