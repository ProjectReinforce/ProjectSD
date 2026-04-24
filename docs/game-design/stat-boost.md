# 시스템 설계: 능력치 (Stat Boost)

> 스킬 외에 캐릭터를 강화하는 부가 시스템. 4등급 체계 기반 선택지.

## 1. 메타

| 항목 | 값 |
|---|---|
| 시스템 ID | `stat_boost` |
| 분류 | 게임플레이 / 성장 |
| 의존 레이어 | Adapter (`Features/Progression/Adapter/Levelupmanager.cs`, `Features/Character/Adapter/PlayerStats.cs`), Domain (`StatType`, `StatModifier`) |
| 최종 업데이트 | 2026-04-24 |

> **SSOT:** 이 문서의 등급 가중치는 `Assets/Data/GameplayConfig.asset` (`defaultRarityWeights`)의 복제본이다.

## 2. 컨셉

스킬과 별도로 캐릭터 자체를 영구 강화하는 보조 성장 축. **만렙 후 레벨업** 또는 **퀘스트 보상** 으로 획득. 4등급 체계 기반 랜덤 선택지로 등장하며, **즉시 캐릭터에 적용**되어 그 Run 안에서 지속된다. ([영구 메타 강화](#5-1-획득-경로) 와는 다름.)

## 3. 게임 규칙

### 3.1 획득 경로

- **레벨업 선택지 — 만렙 시 자동 전환:**
  - 모든 스킬 슬롯이 차고 (6슬롯, [rules.md § 1](rules.md))
  - 모든 스킬이 만렙이면
  - 레벨업 시 스킬 카드 대신 **능력치 부스트 카드 3장** 등장
- **퀘스트 보상:** [quest.md § 보상](quest.md) — 퀘스트 완료 시 능력치 선택지 등장

### 3.2 등급 체계

[overview.md § 12 등급 체계 통합](overview.md) 4단계와 동일.

| 등급 | 확률 | 효과 강도 |
|---|---|---|
| 일반 | 높음 | 낮음 |
| 희귀 | 보통 | 보통 |
| 영웅 | 낮음 | 높음 |
| 전설 | 매우 낮음 | 매우 높음 |

> 구체적인 확률·강도 수치는 밸런싱 단계.

### 3.3 적용 방식

- 획득한 능력치는 **즉시 캐릭터에 적용** (`PlayerStats` 의 `StatModifierCollection` 에 추가).
- **그 Run 동안 영구 유지** (사망/부활해도 사라지지 않음).
- **메타 진행으로 이월되지 않음** (영구 스탯 강화는 [§ 5 영구 강화](#5-1-획득-경로) 참조).

### 3.4 스탯 카테고리

[StatType.cs](../../Assets/Scripts/Features/Character/Domain/ValueObjects/StatType.cs) 의 enum 값을 그대로 사용 (재정의 금지):

```
AttackMultiplier, MoveSpeed, MaxHP,
ProjectileSpeed, ProjectileCount, SkillRange, SkillDuration,
Knockback, HealMultiplier, CritDamage, CooldownReduction, Defense,
ExpMultiplier, CritChance, LifeSteal
```

각 등급별로 **어떤 StatType 이 어떤 ModifierOp 로 어떤 값**을 부여하는지 결정 — TBD.

## 4. 수치

### 4.1 등급별 등장 확률 (현재 SO)

`GameplayConfig.defaultRarityWeights = [60, 25, 12, 3]` — 4등급 체계 공용 (혼돈 스킬·능력치·무기).

| 등급 | 가중치 | 정규화 비율 |
|---|---|---|
| 일반 (Common) | **60** | 60% |
| 희귀 (Rare) | **25** | 25% |
| 영웅 (Epic) | **12** | 12% |
| 전설 (Legendary) | **3** | 3% |

### 4.2 효과 강도 (예시, 실제 StatBoostData SO 미구현)

| 등급 | 효과 강도 안 (예: AttackMultiplier) |
|---|---|
| 일반 | +5% |
| 희귀 | +12% |
| 영웅 | +25% |
| 전설 | +50% |

> 실제 값은 등급 표 + 스탯 종류별 카테고리 매트릭스로 분리될 가능성 (예: AttackMultiplier 는 곱연산, MoveSpeed 는 가산). `StatBoostData` SO는 아직 미작성 — TBD.

## 5. 데이터 계약

### 5.1 StatBoostData (ScriptableObject)

```
Assets/Data/StatBoosts/{boost_id}.asset
StatBoostData : ScriptableObject
  - boostId : string  (예: "boost_attack_legendary")
  - displayName : string
  - rarity : enum { Common, Rare, Epic, Legendary }
  - statType : StatType         // (재사용)
  - op : ModifierOp             // Add / Multiply
  - value : float
  - sprite : Sprite             // 카드 아이콘
```

### 5.2 적용 (Adapter)

```
Features/Character/Adapter/PlayerStats.cs
  - StatModifierCollection 에 StatModifier(boostId, statType, op, value) 추가
  → GetFilteredXxx() 호출 시 자동 반영 (Executor 경유 주입 — 기존 경로 그대로)

Features/Progression/Adapter/Levelupmanager.cs
  - 만렙 판정 후 SkillCard 대신 StatBoostCard 3장 RPC 전송
  - 선택 결과 RPC → 호스트가 PlayerStats 에 적용 → 모든 클라이언트 동기화
```

## 6. 네트워크

[network-sync.md](../systems/network-sync.md) 규약을 따른다.

- **선택지 생성:** 호스트 (4등급 가중치 RNG)
- **선택 결과 전파:** `RPC_ApplyStatBoost(playerViewID, boostId)` → 모든 클라이언트가 PlayerStats 에 동일 적용
- **전원 선택 완료 → 게임 재개:** 기존 LevelUpManager 의 게임 재개 흐름 재사용

## 7. UI / 비주얼

- **카드 UI:** 기존 [SkillCardUI](../../Assets/Scripts/Features/UI/Presentation/) 의 색상 시스템 재사용 (등급별 색상)
- **만렙 후 첫 등장:** 이전 Run 과 다른 알림 (TBD — "All Stats Mode" 같은 안)
- **선택 즉시 반영:** HUD 의 스탯 표기 (있다면) 갱신

## 8. 관련 문서

- [rules.md § 1 스킬 슬롯](rules.md) — 만렙 트리거 조건 정의
- [overview.md § 12 등급 체계](overview.md) — 4등급 체계 SSOT
- [quest.md](quest.md) — 두 번째 획득 경로
- [skill-executor.md](../systems/skill-executor.md) — `applicableStats` 필터 / `PlayerStats.GetFilteredXxx`
- [overview.md § 영구 스탯 강화 검토](overview.md) — EA 후 메타 진행 추가 검토

## 9. 오픈 이슈

- **각 StatType 별 등급 강도 매트릭스** — 어떤 스탯이 어떤 등급에서 얼마를 부여하는지 (밸런싱)
- **부스트 누적 방식** — 같은 스탯 부스트 중복 획득 시 가산? 곱? 캡? (현재 안: `StatModifierCollection` 의 Add/Multiply 합산 그대로)
- **만렙 판정** — 모든 스킬 슬롯이 차야 하는가, 진화로 빈 슬롯이 생기면 다시 스킬 카드로 돌아가는가 ([rules.md § 1](rules.md) 와 일관성 검증)
- **퀘스트 보상의 등장 등급 분포** — 레벨업과 동일? 다른 가중치? ([quest.md](quest.md))
- **영구 스탯 강화 (메타 진행)** — 초기 출시 제외, EA 피드백 후 재검토 ([overview.md § 9](overview.md))
- **선택지 거부 / 리롤** — 4등급 체계에서 마음에 안 드는 카드 처리
