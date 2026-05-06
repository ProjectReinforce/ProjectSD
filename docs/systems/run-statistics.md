# Run Statistics — 인-런 통계 / 결과 화면

한 게임 동안 플레이어별·스킬별로 누적된 통계를 결과 화면에 시각화하는 시스템. 메타 진행도와는 **별도 축** (휘발성, 호스트 권위).

## 1. 메타

| 항목 | 값 |
|---|---|
| 시스템 ID | `run-statistics` |
| 분류 | UI / 게임플레이 |
| 의존 레이어 | Adapter (Photon RPC, MonoBehaviour) |
| 의존 시스템 | [`meta-unlock`](meta-unlock.md) — sourceSkillId 인프라 공유, 일부 통계는 영구 누적에도 사용 |
| 최종 업데이트 | 2026-05-06 |
| 구현 상태 | ⬜ 미구현 — plan: `~/.claude/plans/synchronous-pondering-taco.md` |

## 2. 목적

**왜:** 결과 화면에서 "누구의 빌드가 가장 효율적이었나" 를 시각화. 멀티 협동 플레이의 핵심 만족감 = 자기 빌드 평가 + 동료와 비교. 현재는 팀 합산 킬/사망만 보여주고 있어 정보가 부족.

**왜 sourceSkillId 인프라:** "이번 런에서 어떤 스킬이 가장 강했나" 를 보여주려면 데미지 컨텍스트에 발사 스킬 ID 가 실려야 함. 본 인프라는 [`meta-unlock`](meta-unlock.md) 의 D1 ("특정 스킬로 처치" 조건) 도 사실상 거의 무비용으로 가능하게 한다.

**왜 호스트 권위:** 데미지 적용 자체가 호스트 권위(현재 코드) 라 통계도 호스트에 자연스레 모임. 클라 예측 데미지가 있다면 호스트 확정 데미지만 카운트해서 이중 카운트 방지.

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

## 4. sourceSkillId 인프라 (핵심 비용)

### 데미지 컨텍스트 확장

```csharp
// 기존 DealDamageHandler / DealDamageNearbyHandler 류
public struct DamageContext
{
    public int attackerActorNumber;
    public int sourceSkillId;       // ← 신규
    public float damage;
    // ... 기존 필드
}
```

### 발사 경로

스킬 Executor / Spawner 가 자기 SkillData.skillId 를 컨텍스트에 실어보낸다. 모든 데미지 발사 경로(Projectile / Area / Orbital / Placed / Debuff) 가 일관되게 적용해야 함.

### 통계 누적 (각 클라가 자기 것만)

**기존 인프라 (검증 완료):**
- `RPC_RequestDamage(enemyId, damage, actorNumber)` ([SpawnManager.cs:506](../../Assets/Scripts/Shared/Managers/SpawnManager.cs)) — 클라 → 호스트
- `RPC_NotifyDamageApplied(targetViewId, damage)` ([NetworkAdapter.cs:25](../../Assets/Scripts/Shared/Network/NetworkAdapter.cs)) — 호스트 → 전체
- `RPC_RequestBossDamage(damage)` ([Boss.cs:196](../../Assets/Scripts/Features/Boss/Adapter/Boss.cs)) — 클라 → 호스트 (보스용 별도 흐름)

**페이로드 확장 (필요):**
```csharp
// 기존
RPC_NotifyDamageApplied(int targetViewId, float damage)

// 확장
RPC_NotifyDamageApplied(int targetViewId, float damage,
                       int attackerActorNumber, int sourceSkillId, bool causedDeath)
```

**누적 로직 (모든 클라):**
```csharp
[PunRPC]
private void RPC_NotifyDamageApplied(int targetViewId, float damage,
                                     int attackerActor, int sourceSkillId, bool causedDeath)
{
    // 기존: DamagePopup 등 표시

    // 신규: 자기 통계 누적
    if (attackerActor == PhotonNetwork.LocalPlayer.ActorNumber)
    {
        LocalStatsRecorder.Instance?.AddDamage(sourceSkillId, damage);
        if (causedDeath)
            LocalStatsRecorder.Instance?.OnKill(sourceSkillId);
    }

    // 자기가 받은 데미지면 (targetViewId 가 자기 PlayerHealth 의 viewId 면)
    if (IsLocalPlayerTarget(targetViewId))
        LocalStatsRecorder.Instance?.AddDamageTaken(damage);
}
```

**이중 카운트 방지:** 호스트가 확정한 데미지가 RPC 로 한 번 전파되고, 모든 클라에서 한 번씩 발화 → ActorNumber 매칭 시 한 번만 누적. 클라 예측 데미지는 호스트 확정 RPC 가 아니므로 자동 제외.

**메타 진행도 통합:** `causedDeath=true` 일 때 메타 진행도(`LocalRunStatsRecorder`) 도 같은 RPC 핸들러에서 자기 ActorNumber 매칭 시 OnKill 호출. 별도 사망 RPC 신설 불필요 — `Enemy.OnDiedWithRef` 직접 구독 대신 이 RPC 핸들러를 진입점으로.

**WHY 호스트가 아니라 각 클라:** 마이그레이션 시 호스트 측 dict 가 손실되는 위험 회피. 자기 데이터를 자기가 보유 — 각 플레이어 디바이스별 개별 누적([`meta-unlock.md`](meta-unlock.md) D4) 와 일관된 권위 모델.

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

### 플레이어 카드
- 캐릭터 / 이름 (기존)
- 스킬 빌드 (기존)
- **추가 행:** 킬 · 사망 · 가해 데미지 · 받은 데미지 · DPS(=damageDealt/playTime)

### 스킬별 효율 차트 (플레이어 카드 안)
- 스킬 아이콘 + 누적 데미지 막대 (가로 바 차트)
- 막대 색은 데미지 비중 기준 그라데이션 (가장 강한 스킬 = 가장 진한 색)

### 팀 합계 패널
- 총 킬 · **총 데미지** · 생존 시간 (총 데미지는 신규)

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

- [ ] 2클라가 동일 적을 같이 잡았을 때 막타 가해자 본인의 KillCount 만 +1.
- [ ] 데미지는 가해 비율로 분배되어 두 플레이어의 DamageDealt 합 = 적 처치 시점까지 입은 데미지 총합.
- [ ] 스킬별 차트가 각 플레이어 카드에 표시되고 합계가 `PlayerBuildData.DamageDealt` 와 ±1% 이내 일치.
- [ ] 팀 합계 패널의 총 데미지 = Σ 각 플레이어 DamageDealt.
- [ ] sourceSkillId 가 클라이언트 예측 데미지가 아닌 호스트 확정 데미지에서만 카운트 (이중 카운트 X).
- [ ] **호스트 마이그레이션 시 통계 보존** — 보스 직전 호스트가 나가서 마이그레이션 → 보스 처치 후 결과 화면에 마이그레이션 전 데미지/킬도 정상 표시.
- [ ] 동일 데미지 RPC 가 모든 클라에서 발화될 때 ActorNumber 매칭 필터로 한 번만 카운트 (자기 데미지가 자기 PC 에서만 +1).

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
- [x] **데미지 RPC 가 모든 클라에 발화되는지 — 검증 완료.** `RPC_NotifyDamageApplied` 가 NetworkAdapter.cs:25 에 존재. 페이로드 확장만으로 분산 추적 가능.
