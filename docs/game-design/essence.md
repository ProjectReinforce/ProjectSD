# 시스템 설계: 정수 (Essence)

> 스킬에 속성을 부여하는 빌드 다양화 시스템. 엘리트 적의 핵심 보상.

## 1. 메타

| 항목 | 값 |
|---|---|
| 시스템 ID | `essence` |
| 분류 | 게임플레이 / 빌드 |
| 의존 레이어 | Features/Essence/{Domain,Adapter}, Features/Skill/Adapter/TriggerEffects/, Features/Character/Adapter (PlayerStats) |
| `source` prefix | `essence_{type}_{slotIndex}` (슬롯별 구분) — [trigger-effects.md § 5](../systems/trigger-effects.md) |
| 구현 상태 | ✅ Phase 3 완료 (커밋 `a723d9f79`) |
| 최종 업데이트 | 2026-04-24 |

> **SSOT:** 이 문서의 수치는 `Assets/Data/Pickup/{Fire,Ice,Lightening}EssenceData.asset` 및 `Assets/Data/EliteDropTable.asset` / `EnemyDropTable.asset` 의 복제본이다.

## 2. 컨셉

스킬에 **속성**을 부여해 같은 스킬이 다른 방식으로 작동하게 한다. 빌드 다양성을 확보하는 핵심 요소이며, **엘리트 적 처치를 우선 행동으로 만들기 위한 보상 장치**.

## 3. 게임 규칙

### 3.1 획득

- **드랍원:** [엘리트형](enemies/elite.md) 적이 일정 확률로 드랍. 엘리트 등장 빈도는 밸런싱 단계.
- **선착순:** 모든 플레이어에게 보이며, 먼저 줍는 사람이 획득.
- **보유 한계:** 한 플레이어 **최대 2개**.
  - 이미 2개 보유 시 **추가 획득 불가** (드랍은 그대로 보이지만 줍기 차단).

### 3.2 속성 종류

| 속성 | 효과 | 핸들러 매핑 (Phase 3 구현 확정) |
|---|---|---|
| **얼음** | 적 슬로우 | `OnHit → ApplySlow` (`primary=배율 0~1`, `secondary=지속초`) — EnemyMovement.slowStack 곱셈 중첩 지원 |
| **불** | 화상 — 도트 데미지 | `OnHit → ApplyDoT` (`primary=틱 데미지`, `secondary=지속초`, `tertiary=틱 간격`) — DoTEffect 다중 인스턴스 (source 별 공존) |
| **번개** | 적중 주변 N마리에 고정 데미지 | `OnHit → DamageNearby` (`primary=반경`, `secondary=대상 수`, `tertiary=데미지`) — **신규 핸들러**. Chain 과 다름 — 선형 전이 아니라 방사형 동시 타격 |

### 3.3 중첩 / 시너지 (Phase 3 구현 확정, 현재 SO 값)

같은 속성 2개 장착 시 동작:
- **Stack2 시너지 미정의 (EssenceData.injectedEffectsStack2 비어있음)**: 슬롯 0/1 효과가 **각각 독립 발동** → 자연 합산 (ApplyDoT/ApplySlow/DamageNearby 모두 source 별 독립 인스턴스).
- **Stack2 시너지 정의**: 슬롯 1 효과는 등록 안 함 + 슬롯 0 의 1스택 효과를 Stack2 파라미터로 교체. 총 효과 = Stack2 1회분 (비선형 시너지).

**현재 SO 파라미터 (`FireEssenceData` / `IceEssenceData` / `LighteningEssenceData`):**

| 속성 | Trigger | Action | 1스택 (primary / secondary / tertiary) | 2스택 (Stack2) |
|---|---|---|---|---|
| 불 (`FireEssenceData`, type=1) | OnHit | ApplyDoT (3) | **4 / 3 / 1** (틱 데미지 4, 지속 3s, 틱간격 1s) | **10 / 3 / 1** (틱 데미지 10, 지속 3s, 틱간격 1s → 총 데미지 2.5배) |
| 얼음 (`IceEssenceData`, type=0) | OnHit | ApplySlow (4) | **0.7 / 3 / 0** (이속 배율 0.7 = 30% 슬로우, 지속 3s) | **0.7 / 2.5 / 0** (지속 2.5s로 감소 — 현재 SO 값) |
| 번개 (`LighteningEssenceData`, type=2) | OnHit | DamageNearby (11) | **1 / 2 / 6** (반경 1, 대상 2마리, 6 데미지) | **1 / 6 / 9** (반경 1, 대상 6마리, 9 데미지 → 총 3배 효과) |

⚠️ 얼음 2스택의 `secondary` 가 3 → 2.5로 **감소**하고 있음 — 의도 재검토 필요(문서 3.3의 "지속 1.5배" 서술과 상반).

### 3.4 조합 효과 (서로 다른 속성, TBD)

2개의 서로 다른 속성을 보유하면 두 속성이 동시에 발현된다. **특정 조합**에서는 히든 효과 발동 (구현 예정).

| 조합 | 기본 동시 발현 | 히든 효과 안 |
|---|---|---|
| 얼음 + 번개 | 슬로우 + 방사 | 슬로우 걸린 적에게 번개는 **치명타** 처리 |
| 얼음 + 불 | _상충 처리 미정_ | TBD (기획) |
| 불 + 번개 | 화상 + 방사 | TBD (기획) |

## 4. 수치 (현재 SO 값)

### 4.1 드랍 확률

| 소스 | `essenceChance` | 타입 가중치 (`essenceTypeWeights`) |
|---|---|---|
| 일반 적 (`EnemyDropTable`) | **0** (드랍 없음) | — |
| 엘리트 (`EliteDropTable`) | **1.0 (100% 드랍)** | `[1, 1, 1]` (Fire/Ice/Lightning 동등) |

### 4.2 정수별 효과 파라미터

§ 3.3 의 SO 값 테이블 참조.

### 4.3 보유 한계

- 한 플레이어 **최대 2개** (코드 측 상수).

## 5. 데이터 계약

### 5.1 드랍 (실제 구현)

`EnemyDropTable` / `EliteDropTable` SO가 확률/가중치를 관리. `EnemyData.dropTable` 필드가 두 테이블 중 하나를 참조.

```csharp
// Shared/Data/EnemyDropTable.cs
public float essenceChance;               // 0.0~1.0
public float[] essenceTypeWeights;        // Ice(0)/Fire(1)/Lightning(2) 가중치
```

### 5.2 정수 SO (`EssenceData`)

```csharp
// Features/Essence/Adapter/Data/EssenceData.cs
public EssenceType type;                                   // Ice=0, Fire=1, Lightning=2
public SkillTriggerEffect[] injectedEffects;               // 1스택
public SkillTriggerEffect[] injectedEffectsStack2;         // 2스택 (비어있으면 독립 합산)
```
SO 파일: `Assets/Data/Pickup/{Fire,Ice,Lightening}EssenceData.asset`.

### 5.2 런타임 주입 (정수 장착 시)

[trigger-effects.md § 5](../systems/trigger-effects.md) 의 `AddRuntimeEffect` 규약을 그대로 사용. 재정의 금지.

```csharp
// 불 정수 장착
triggerSystem.AddRuntimeEffect("essence_fire", new SkillTriggerEffect(
    TriggerType.OnHit,
    EffectActionType.ApplyDoT,
    new EffectParams(ticks: 3, interval: 1.0f, ratio: 0.4f)
));

// 정수 해제
triggerSystem.RemoveRuntimeEffects("essence_fire");
// 모든 정수 효과 해제
triggerSystem.RemoveByPrefix("essence_");
```

조합 히든 효과는 **별도 source 키**로 추가:
```csharp
// 얼음+번개 조합 발현 시
triggerSystem.AddRuntimeEffect("essence_combo_ice_lightning", ...);
```

## 6. 네트워크

[network-sync.md](../systems/network-sync.md) 규약을 따른다.

- **드랍 판정:** 호스트
- **선착순 처리:** 호스트 (동일 드랍에 두 명 동시 진입 시 ViewID 우선)
- **장착 / 해제 RPC:** `RPC_EquipEssence(playerViewID, essenceType)` → 각 클라이언트가 자신의 SkillTriggerSystem 에 `AddRuntimeEffect` 호출

## 7. UI / 비주얼

- **드랍 비주얼:** 속성별 색상 (얼음 청, 불 적, 번개 황)
- **HUD 표시:** 보유 정수 2개를 HUD 한쪽에 아이콘으로 노출
- **2개 보유 시 추가 드랍:** 비주얼은 보이되 줍기 키가 회색 처리 (TBD)

## 8. 관련 문서

- [enemies/elite.md](enemies/elite.md) — 정수 드랍의 유일한 소스
- [trigger-effects.md § 5](../systems/trigger-effects.md) — 런타임 주입 규약 (SSOT)
- [overview.md § 12 등급 체계](overview.md) — 정수는 등급 체계 적용 대상 아님 (속성 3종만)
- [weapon.md](weapon.md) — 동일 패턴의 자매 시스템

## 9. 오픈 이슈

- **상충 조합 (얼음+불) 처리** — 동시 발현? 한쪽 무효화? 새 효과로 변환? (기획)
- 얼음 슬로우용 **신규 EffectActionType** 필요 여부 (현재 `ApplyDoT` 파생으로 처리 안 vs `ApplySlow` 신규)
- 정수 드랍이 보스에서도 발생하는지 (현재 안: 엘리트만)
- 조합 히든 효과 9개 중 6개 (얼음+불 / 불+번개 외) 의 구체 효과
- 같은 정수 2개 (예: 불+불) 획득 가능 여부 — 현재 안: 가능, 효과 중첩 (TBD)

## 10. 정수 데미지 스케일링 — 설계 TBD (보류)

> **2026-04-25 기록:** 정수의 OnHit DoT/Slow/DamageNearby 데미지가 현재 SO `parameters.primary` 를 그대로 사용 — 플레이어 ATK/CritChance/장착 무기 영향 없음. 후반 스케일링이 안 되어 활용도가 급락하는 문제 있음. 구현 전 아래 결정 필요.

### 결정 항목

| # | 질문 | 옵션 | 영향 |
|---|---|---|---|
| 1 | 플레이어 ATK 가 정수 데미지에 곱해지나? | A: 항상 곱함 (PlayerStats.ApplyAttackTo 경유) / B: 정수 SO 의 `affectedByAtk` 플래그로 제어 / C: 곱 안 함 | A=후반 강력 / B=세밀 제어 / C=일관 데미지 |
| 2 | CritChance / CritDamage 적용? | A: 적용 / B: 미적용 | 정수 시너지 — 무기 크리트 스탯이 정수 DoT 에도 작용하나 |
| 3 | 무기 `WeaponStatEntry.AttackMultiplier` 가 정수 데미지에 영향? | A: 영향 / B: 무기는 무기 스킬에만, 정수는 별개 | LoL 식 빌드 다양성 vs 시스템 분리도 |
| 4 | Triggering skill 의 `damagePerLevel` 도 반영? | A: 반영 (기본 데미지 + 정수 보너스) / B: 정수는 독립 데미지 | "큰 스킬 → 큰 정수 데미지" 직관 vs 정수 SO 수치의 명시성 |
| 5 | 슬로우/넉백 등 비데미지 효과는? | A: 그대로 (수치 영향 없음) / B: 강도(%/거리) 도 ATK 비례 | 비데미지 효과까지 손대면 밸런싱 부담 |

### 영향 범위 (구현 시 손댈 곳)

- `Features/Skill/Adapter/TriggerEffects/Handlers/ApplyDoTHandler.cs:30` — `parameters.primary` 직접 사용 → 스탯 경유
- `Features/Skill/Adapter/TriggerEffects/Handlers/ApplySlowHandler.cs` — 슬로우 강도 결정 시 (옵션 5)
- `Features/Skill/Adapter/TriggerEffects/Handlers/DamageNearbyHandler.cs` — 동일 패턴
- `Features/Skill/Adapter/TriggerEffects/TriggerContext.cs` — `PlayerStats` 참조 노출 (현재 source 만 보유)
- `Features/Character/Adapter/PlayerStats.cs:ApplyAttackTo` — 진입점 활용 (이미 무기/혼돈/패시브 통합 경로)

### 대기 조건

- 위 5 결정이 합의되면 1~1.5 시간 분량 코드 작업.
- 영향 회귀 범위: 정수 3 종 (불 DoT / 얼음 슬로우 / 번개 DamageNearby) 모두 재테스트 필요.
- 관련 로드맵: [../architecture/drop-system-roadmap.md](../architecture/drop-system-roadmap.md) "보류 작업" 섹션.
