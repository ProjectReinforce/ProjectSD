# 시스템 설계: 추가 아이템 (Pickup Items)

> 정수 / 무기 외의 모든 인게임 픽업 아이템. 자석·물약·경험치 오브.
> 정수는 [essence.md](essence.md), 무기는 [weapon.md](weapon.md) 참조.

## 1. 메타

| 항목 | 값 |
|---|---|
| 시스템 ID | `items_pickup` |
| 분류 | 게임플레이 / 드랍 |
| 의존 레이어 | Adapter (`Features/Progression/Adapter/ExperienceOrb.cs` 패턴 재사용) |
| 최종 업데이트 | 2026-04-19 |

## 2. 컨셉

전투 중 적이 드랍하거나 맵에 배치된 보조 자원. 플레이어의 생존·성장을 가속하지만 정수·무기처럼 빌드를 직접 바꾸지는 않는다. **모두 같은 픽업 패턴**을 공유 → 단일 베이스 클래스로 처리.

## 3. 게임 규칙

| 아이템 | 효과 | 드랍 / 등장 |
|---|---|---|
| **경험치 오브** | 처치한 적의 경험치 부여. 자석 흡수 가능. | 모든 적 처치 시 드랍 (확정) |
| **자석** | 맵에 남아 있는 경험치 조각을 **전부** 끌어와 획득 | TBD (낮은 확률) |
| **물약** | 체력 소량 회복. **"체력 회복량 증가" 패시브와 시너지** (`StatType.HealMultiplier`) | TBD (낮은 확률) |

- **흡수 범위:** 경험치 오브는 플레이어 5m 이내 진입 시 자석처럼 끌려옴 ([rules.md § 2](rules.md))
- **흡수 범위 확장:** 패시브 스킬로 확장 가능 (`StatType.SkillRange` 또는 별도 `StatType.PickupRange` 검토)
- **팀 공유:** 경험치는 팀 공유 ([rules.md § 2](rules.md)). 물약은 줍는 플레이어만 회복 (TBD).

## 4. 수치

> _TBD (밸런싱)_

| 항목 | 값 안 |
|---|---|
| 경험치 오브 흡수 시작 거리 | 5m (확정) |
| 자석 드랍 확률 | TBD |
| 물약 드랍 확률 | TBD |
| 물약 기본 회복량 | TBD (PlayerHealth.MaxHP 의 X% 안) |
| HealMultiplier 적용 | `회복량 = base × HealMultiplier` |

## 5. 데이터 계약

```
ScriptableObject 안:
  PickupItemData (Shared/Data/Pickups/{name}.asset)
    - itemId : string  (예: "pickup_magnet", "pickup_potion")
    - displayName : string
    - sprite : Sprite
    - pickupRange : float (자석 흡수 시작 거리)
    - effectType : enum { Magnet, Potion, Experience }
    - effectValue : float (회복량 등)

기존 자산:
  Assets/Scripts/Features/Progression/Adapter/ExperienceOrb.cs  ← 픽업 베이스 패턴
  Assets/Scripts/Features/Character/Adapter/PlayerHealth.cs     ← 회복 호출부
```

물약 회복 시 `PlayerHealth.Heal(value × StatModifier(HealMultiplier))` — `StatModifierCollection` 으로 합산.

## 6. 네트워크

[network-sync.md](../systems/network-sync.md) 규약을 따른다.

- **드랍 판정:** 호스트
- **선착순 처리:** 호스트 (동일 아이템에 두 명 동시 진입 시 먼저 도달한 ViewID 우선)
- **회복/경험치 적용:** 호스트가 RPC 로 해당 플레이어에게 적용
- **자석 발동:** 호스트가 `RPC_TriggerMagnet(playerViewID)` 호출 → 모든 클라이언트가 시각 효과 + 경험치 오브 흡수 시작

## 7. UI / 비주얼

- **자석:** 픽업 시 화면 전체 경험치 오브가 플레이어에게 빨려 들어가는 강한 시각 효과
- **물약:** 픽업 시 PlayerVisual 에 짧은 회복 이펙트 + HUD 체력 바 변화
- **경험치 오브:** 색상으로 등급 구분 가능성 (TBD)

## 8. 관련 문서

- [rules.md § 2 경험치](rules.md) — 팀 공유, 흡수 범위
- [rules.md § 8 아이템/드랍](rules.md) — 드랍 규칙 요약
- [essence.md](essence.md), [weapon.md](weapon.md) — 별도 시스템

## 9. 오픈 이슈

- 자석 / 물약 드랍 확률 (밸런싱)
- 물약 회복량의 기본값 (절대값 vs MaxHP 비율)
- 물약을 줍는 플레이어만 회복인지, 팀 공유 회복인지 (현재 안: 줍는 사람만)
- 흡수 범위 확장이 `SkillRange` 와 별도 스탯이 필요한지 (현재 안: `SkillRange` 재활용)
- 자석 / 물약 외 기타 아이템 추가 여부 (보물 상자 등 — 향후 확장)
