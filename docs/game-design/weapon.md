# 시스템 설계: 무기 (Weapon)

> 캐릭터에게 추가 능력치를 부여하는 장비 시스템. 스킬 종류별로 적합한 장비가 존재.

## 1. 메타

| 항목 | 값 |
|---|---|
| 시스템 ID | `weapon` |
| 분류 | 게임플레이 / 빌드 / 장비 |
| 의존 레이어 | Adapter (`Features/Skill/Adapter/TriggerEffects/`), Data (WeaponData), Character (StatModifier) |
| `source` prefix | `weapon_*` — [trigger-effects.md § 5](../systems/trigger-effects.md) |
| 최종 업데이트 | 2026-04-19 |

## 2. 컨셉

LoL 아이템처럼 장비를 장착해 능력치를 추가하고, 인벤토리 조합을 통해 더 강한 장비로 변환한다. **스킬 유형(투사체형 / 설치형 등)마다 적합한 장비**가 있어, 자신의 빌드 방향에 맞는 장비를 우선 선택하게 만든다. 정수가 "속성"을 부여한다면, 무기는 "스탯과 시너지"를 부여한다.

## 3. 게임 규칙

### 3.1 획득

- **드랍원:** 모든 적이 **매우 낮은 확률**로 드랍.
- **선착순:** 모든 플레이어에게 보이며, 먼저 줍는 사람이 획득.

### 3.2 줍기 UX

- 드랍된 무기에 **가까이 접근하면** 인벤토리 조합 결과가 UI 로 표시:
  - 현재 인벤토리 + 이 무기 → **조합 결과** 가 어떤 무기인지 미리 보임
  - 조합 결과가 **없으면 UI 가 출력되지 않음** (기본 줍기만 가능)
- **줍기 키** 입력 시 획득 / 조합 실행

### 3.3 인벤토리 / 슬롯

- **장비 슬롯 4개** (밸런싱 단계에서 수정 가능)
- 슬롯이 모두 차고 조합도 불가능하면 **줍기 차단** (기존 무기 버리기는 TBD)

### 3.4 조합 시스템

- **인벤토리 슬롯 순서대로 조합 체크** (앞 칸부터 순차적으로 매칭).
- 유저가 원하는 조합을 **전략적으로 우선 배치**하도록 유도 (의도적 디자인).
- 조합이 성립하면 **재료 슬롯이 비워지고 결과 무기가 들어옴** (LoL 와 동일 모델).
- 같은 무기를 다시 만들 수 있는지 (분해/이동) — TBD.

### 3.5 스킬 유형 ↔ 장비 매핑

> _TBD (기획) — 무기 종류 자체가 미정._ 다음은 안:

| 스킬 유형 | 적합 장비 카테고리 안 |
|---|---|
| 투사체형 (표창, 매직 미사일 등) | 투사체 속도/관통 위주 |
| 설치형 (자동포탑, 지뢰 등) | 쿨다운/지속시간 위주 |
| 회전형 (장검) | 공격력/범위 위주 |
| 장판형 (성역, 개미지옥) | 범위/지속시간 위주 |
| 디버프형 | 디버프 강도/지속 위주 |

## 4. 수치

> _TBD (밸런싱)_

| 항목 | 값 안 |
|---|---|
| 드랍 확률 (모든 적 공통) | TBD (매우 낮음) |
| 장비 슬롯 수 | **4개** (확정, 밸런싱에서 조정) |
| 무기 종류 수 | TBD (초안 5~8종) |
| 조합 레시피 수 | TBD |
| 부여 가능 스탯 | `StatType` enum 전체 (특히 `CritChance`, `LifeSteal`, `AttackMultiplier`) |

## 5. 데이터 계약

### 5.1 WeaponData (ScriptableObject)

```
Assets/Data/Weapons/{weapon_id}.asset
WeaponData : ScriptableObject
  - weaponId : string  (예: "weapon_blade_01")
  - displayName : string
  - sprite : Sprite
  - rarity : enum { Common, Rare, Epic, Legendary }   // 4등급 체계
  - statModifiers : StatModifier[]                     // (StatType, ModifierOp, value)
  - triggerEffects : SkillTriggerEffect[] (선택)       // weapon_* source 로 주입
  - combineRecipe : WeaponCombineRecipe                // 결과 무기 + 재료
  - skillTypeAffinity : SkillType[] (선택)             // 적합 스킬 유형 표시용

WeaponCombineRecipe:
  - inputs : WeaponData[]   // 필요 재료 무기 (인벤토리 순서대로 매칭)
  - output : WeaponData     // 조합 결과
```

### 5.2 인벤토리 / 장착 (Adapter)

```
Features/Character/Adapter/PlayerWeaponInventory.cs (신규 안)
  - slots : WeaponData[4]
  - bool TryAddOrCombine(WeaponData drop) → (success, resultWeapon)
  - void RemoveAt(int index)

장착 시 호출 경로:
  WeaponData.statModifiers → PlayerStats.StatModifierCollection 추가
  WeaponData.triggerEffects → SkillTriggerSystem.AddRuntimeEffect("weapon_{id}", ...)

해제 시:
  PlayerStats.StatModifierCollection 제거
  SkillTriggerSystem.RemoveByPrefix("weapon_{id}")
```

기존 `Features/Character/Domain/ValueObjects/StatType.cs` 의 `CritChance`, `LifeSteal` 항목은 무기/정수 시스템용으로 이미 예약되어 있음 — 재정의 금지.

## 6. 네트워크

[network-sync.md](../systems/network-sync.md) 규약을 따른다.

- **드랍 판정:** 호스트
- **줍기 판정 / 조합 결과 계산:** 호스트
- **선착순 처리:** 호스트
- **장착 RPC:** `RPC_EquipWeapon(playerViewID, weaponId, slotIndex)` → 각 클라이언트가 자신의 PlayerStats / SkillTriggerSystem 갱신
- **조합 RPC:** `RPC_CombineWeapon(playerViewID, slotIndices[], outputWeaponId)`

호스트가 인벤토리 슬롯 순서를 권위적으로 보유하므로, 클라이언트 UI 미리보기와 호스트 결과가 다를 가능성에 주의 (보통 동일 — 슬롯 순서는 결정적).

## 7. UI / 비주얼

- **드랍 비주얼:** 등급별 색상 (4등급 체계 [overview.md § 12](overview.md))
- **접근 시 미리보기 UI:** 화면 한쪽에 "현재 인벤토리 + 이 무기 → 조합 결과: {무기명}" 표시. 결과 없으면 UI 미출력.
- **인벤토리 HUD:** 장비 슬롯 4칸을 HUD 에 상시 표시 (스킬 슬롯과 별도)
- **줍기 키:** 캐릭터 컨트롤 키 매핑 ([Player.cs](../../Assets/Scripts/Features/Character/Adapter/Player.cs)) — 신규 입력 추가 필요

## 8. 관련 문서

- [trigger-effects.md § 5](../systems/trigger-effects.md) — `weapon_*` 런타임 주입 규약 (SSOT)
- [essence.md](essence.md) — 동일 패턴의 자매 시스템 (속성 부여)
- [stat-boost.md](stat-boost.md) — 4등급 체계 / StatType 공유
- [overview.md § 12](overview.md) — 등급 체계 통합

## 9. 오픈 이슈

- **무기 종류** — 5~8종 초안 + 카테고리 분류 (기획)
- **조합 레시피** — 예: A+B → C 의 구체 매트릭스 (기획)
- **분해 / 버리기** — 슬롯 4개 모두 차고 조합 불가능한 드랍 처리 (현재 안: 줍기 차단)
- **재료 무기의 능력치 합산** — 조합 결과 무기가 재료 능력치를 일부 흡수하는지, 완전 교체인지
- **스킬 유형별 장비 매핑** — 투사체형/설치형/회전형/장판형/디버프형 등 카테고리 명세
- **줍기 키 입력 처리** — 별도 키 vs 자동 줍기 (현재 안: 별도 키)
- **같은 무기 중복 보유** — 슬롯 중복 허용? 단순 강화? (TBD)
