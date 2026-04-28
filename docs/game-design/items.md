# 시스템 설계: 추가 아이템 (Pickup Items)

> 정수 / 무기 외의 모든 인게임 픽업 아이템. 자석·물약·경험치 오브.
> 정수는 [essence.md](essence.md), 무기는 [weapon.md](weapon.md) 참조.

## 1. 메타

| 항목 | 값 |
|---|---|
| 시스템 ID | `items_pickup` |
| 분류 | 게임플레이 / 드랍 |
| 의존 레이어 | Adapter (`Features/Progression/Adapter/ExperienceOrb.cs` 패턴 재사용) |
| 최종 업데이트 | 2026-04-29 (R3 마이크 필터 픽업 ✅ 구현 완료) |

> **SSOT:** 이 문서의 수치는 `Assets/Data/GameplayConfig.asset` 과 `Assets/Data/EnemyDropTable.asset` / `EliteDropTable.asset` 의 복제본이다.

## 2. 컨셉

전투 중 적이 드랍하거나 맵에 배치된 보조 자원. 플레이어의 생존·성장을 가속하지만 정수·무기처럼 빌드를 직접 바꾸지는 않는다. **모두 같은 픽업 패턴**을 공유 → 단일 베이스 클래스로 처리.

## 3. 게임 규칙

| 아이템 | 효과 | 일반 적 드랍 | 엘리트 드랍 |
|---|---|---|---|
| **경험치 오브** | 처치한 적의 경험치 부여. 자석 흡수 가능. | 모든 적 처치 시 드랍 (확정) | 동일 |
| **자석** | 맵에 남아 있는 경험치 조각을 **전부** 끌어와 획득 | `magnetChance = 0.01` (1%) | 0 (엘리트는 자석 안 드랍) |
| **물약** | 체력 소량 회복. **"체력 회복량 증가" 패시브와 시너지** (`StatType.HealMultiplier`) | `potionChance = 0.01` (1%) | 0 |
| **마이크 필터 (R3)** | 카오스 재미. 호스트가 활성 플레이어 중 랜덤 1명(본인 포함) 선택 + 5종 필터 중 랜덤 1종 적용. 일정 시간 후 자동 해제. | `micFilterChance = 0.005` (0.5% 권장) | 0 |

- **흡수 범위:** 경험치 오브는 `GameplayConfig.magnetRange = 0.7` 이내 진입 시 자석처럼 끌려옴. 끌어당기는 속도는 `magnetSpeed = 2`.
- **흡수 범위 확장:** 패시브 스킬로 확장 가능 (`StatType.SkillRange` 또는 별도 `StatType.PickupRange` 검토).
- **경험치 오브 상한:** `maxActiveExpOrbs = 200` (동시 활성 개수 제한, 프리워밍 80).
- **팀 공유:** 경험치는 팀 공유 ([rules.md § 2](rules.md)). 물약은 줍는 플레이어만 회복 (TBD).

## 4. 수치 (현재 SO 값)

| 항목 | 값 | 출처 |
|---|---|---|
| 경험치 오브 흡수 시작 거리 | **0.7** | `GameplayConfig.magnetRange` |
| 경험치 오브 흡수 속도 | **2** | `GameplayConfig.magnetSpeed` |
| 경험치 오브 동시 상한 | **200** | `GameplayConfig.maxActiveExpOrbs` |
| 경험치 오브 프리워밍 | **80** | `GameplayConfig.expOrbPrewarmCount` |
| 자석 드랍 확률 (일반) | **0.01 (1%)** | `EnemyDropTable.magnetChance` |
| 물약 드랍 확률 (일반) | **0.01 (1%)** | `EnemyDropTable.potionChance` |
| 물약 기본 회복량 | TBD (PlayerHealth.MaxHP 의 X% 안) — 별도 PickupItemData SO 신설 필요 |
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

## 8-1. 마이크 필터 픽업 (R3) — 카오스 아이템

**컨셉:** Photon Voice 송신 음성을 일정 시간 변형. **본인은 자기 음성을 못 듣는다**(Photon Voice self-mute) → 다른 사람들이 "어 너 마이크 왜 그래?" 반응에서 깨닫는 게 본질적 재미.

**5종 필터 (MicFilterType enum):**

| Type | 효과 | Unity 컴포넌트 / 속성 | 인스펙터 핵심 파라미터 |
|---|---|---|---|
| `LowPass` | 먹먹/물 속 | `AudioLowPassFilter` | `cutoffFrequency` (Hz, 1500↓ 명확) |
| `Distortion` | 깨진 마이크 | `AudioDistortionFilter` | `distortionLevel` (0~1, 0.5↑ 명확) |
| `Echo` | 동굴 메아리 | `AudioEchoFilter` | `delay` (ms), `decayRatio`, `dryMix`, `wetMix` |
| `PitchHelium` | 헬륨 고음 | `AudioSource.pitch` | `pitchValue` (1.5~1.8) |
| `PitchDemon` | 악마 저음 | `AudioSource.pitch` | `pitchValue` (0.6~0.7) |

**디자인 결정 기록:**
- **드랍 소스:** 모든 적 매우 낮은 확률 (`EnemyDropTable.micFilterChance`, 0.005 기본)
- **타겟:** 본인 포함 활성 플레이어 중 랜덤 1명 (호스트 권위)
- **지속 시간:** 인스펙터 노출 (`MicFilterData.durationSeconds`, 기본 15초)
- **겹침 처리:** 새 필터로 즉시 교체. 같은 사람에게 같은 필터 또 걸리면 자동 시간 연장 효과
- **시각 표시:** 없음. 본인/타인 모두 안내 0 — 카오스 재미 유지

**구현 위치:**
- `Features/Voice/Domain/MicFilterType.cs` — 5종 enum
- `Features/Voice/Adapter/Data/MicFilterData.cs` — SO (필터별 파라미터 + 지속 시간)
- `Features/Voice/Adapter/Data/MicFilterDatabase.cs` — SO 루트
- `Features/Voice/Adapter/MicFilterController.cs` — PlayerStub 부착, RPC 수신 + AudioFilter 동적 add/destroy + 만료 코루틴
- `Features/Voice/Adapter/MicFilterPickup.cs` — `PickupItemBase` 즉시 발동
- `Features/Pickup/Adapter/DropSpawner.cs` — `RaiseMicFilterApplied` + `RPC_ApplyMicFilter`
- `Shared/Data/EnemyDropTable.cs` — `micFilterChance` 필드
- `Shared/Managers/GameManager.cs` — `MicFilterDB` 슬롯

**네트워크 흐름:**
```
호스트 측 MicFilterPickup.OnPickedUpByPlayer
  → 활성 ActorNumber 랜덤 선택 + filterIdx 랜덤 롤
  → DropSpawner.RaiseMicFilterApplied(actor, idx, dur)
  → photonView.RPC(RPC_ApplyMicFilter, All)
모든 클라 (호스트 포함)
  → 자기 측 PlayerStub 들 중 ActorNumber 매칭
  → MicFilterController.ApplyFilter(idx, dur)
  → AudioFilter 컴포넌트 동적 추가 (또는 AudioSource.pitch 변경)
  → 클라 자체 코루틴이 dur 후 ClearFilterImmediate
```

**만료 권위:** 클라 자체. 시작 시점만 RPC 동기화 (100ms 차이 무관). 호스트 마이그레이션 시에도 영향 0.

**검증:**
- **R3 효과 검증은 ParrelSync 2 인스턴스로 충분** — Phase 8-2 와 동일 음성 인프라 위에서 동작. 한 인스턴스에서 V (PTT) 로 송출하면 다른 인스턴스 측에서 필터 변형된 음성이 들림.
- ParrelSync 4 인스턴스 동시 송출은 같은 OS 마이크 공유로 누가 누구를 듣는지 살짝 헷갈림 → 검증 시 2 인스턴스 권장.
- **빌드 환경 종합 송수신 검증 (다른 PC/OS/마이크, 호스트 마이그레이션 중 끊김 등) 은 R3 와 별건**. Stove/Steam 출시 전 1회 수행. R3 커밋 블로커 아님.

**알려진 한계 (수용):**
- **타겟 race window** — 호스트가 ActorNumber 결정 → RPC 도달 사이 그 플레이어가 룸을 이탈하면 모든 측이 매칭 실패 + LogWarning 후 픽업 소실. fallback 재롤은 무한 재귀 위험으로 미적용. 100~500ms 윈도우라 매우 드물고 카오스 재미 영향 미미.
- **PUN spoofing** — RPC_ApplyMicFilter 자체 권한 검증 없음(자석/물약과 동일 수준). 인디 게임 보안 모델상 수용.

## 9. 오픈 이슈

- ~~자석 / 물약 드랍 확률~~ — SO에 0.01 기본값 입력됨 (밸런싱에서 재조정)
- 물약 회복량의 기본값 (절대값 vs MaxHP 비율) — `PickupItemData` SO 설계 필요
- 물약을 줍는 플레이어만 회복인지, 팀 공유 회복인지 (현재 안: 줍는 사람만)
- 흡수 범위 확장이 `SkillRange` 와 별도 스탯이 필요한지 (현재 안: `SkillRange` 재활용)
- 자석 / 물약 외 기타 아이템 추가 여부 (보물 상자 등 — 향후 확장)
