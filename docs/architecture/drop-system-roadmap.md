# 드랍 시스템 구현 로드맵 — 정수/무기/퀘스트/능력치/기타아이템/혼돈등급

> 2026-04-21 승인본. 이 문서는 "월드에 뭔가를 떨구고, 주워서 효과를 받는" 시스템 6종을 Phase 단위로 구현하기 위한 공식 로드맵이다. 진행 상황은 각 Phase 의 체크박스를 업데이트한다.

## Context

현재 ProjectSD는 "적 사망 → 경험치 오브(ExperienceOrb) → 플레이어 흡수 → XP 획득" 플로우 하나만 구현되어 있다. 설계 문서(`docs/game-design/essence.md`, `weapon.md`, `quest.md`, `stat-boost.md`, `items.md`)는 완성되어 있으나, 월드에 떨어지는 드랍 아이템 자체와 주워서 효과를 받는 경로가 비어 있다. 이 로드맵은 그 공백을 채우는 작업을 Phase 단위로 쪼갠 것.

결정된 방향:
- **혼돈 등급**: 혼돈 스킬 19종에 `Rarity` enum 적용. 무기/능력치/혼돈이 같은 4등급(Common/Rare/Epic/Legendary) 체계 공유.
- **폴더 조직**: 독립 Feature + 공통 Pickup Feature. `Features/{Essence,Weapon,Quest,StatBoost}/` 각각 Feature-first 레이어, 월드 픽업 베이스는 `Features/Pickup/`.
- **구현 순서**: 인프라 → 기존 ExpOrb 리팩 → 기타(자석/물약) → 정수 → 무기 → 능력치 → 퀘스트 → 혼돈 등급.
- **드랍 대상과 비드랍 대상 구분**:
  - 월드 드랍으로 입수: 경험치 오브, 자석, 물약, 정수(엘리트만), 무기
  - 월드 드랍 아님: **능력치(StatBoost)** = 만렙 후 레벨업 선택 + 퀘스트 완료 보상. **퀘스트** = 맵 거점. **혼돈 스킬** = Lv.10/20/30 선택.
- **등급 선정 규칙**: 선택지 카드 3장은 **모두 동일 등급**. 먼저 Rarity를 가중치로 롤 → 해당 등급 풀에서 3장 중복 없이 샘플링. 혼돈·능력치·(필요 시 무기 조합 미리보기) 모두 같은 공통기 사용.

---

## 재사용 가능한 기존 자산 (확정)

| 대상 | 파일:라인 | 용도 |
|---|---|---|
| `ExperienceOrb` | `Assets/Scripts/Features/Progression/Adapter/ExperienceOrb.cs` | `IPoolable` + 자석 흡수 + 호스트 권위 판정 + GameState 체크 — 모든 픽업 아이템의 베이스 템플릿 |
| `PlayerStats.AddModifier / Recalculate` | `Assets/Scripts/Features/Character/Adapter/PlayerStats.cs:120` | 무기·능력치 스탯 부여 단일 경로. source 접두사 컨벤션(`essence_/weapon_/stat_`) |
| `SkillTriggerSystem.AddRuntimeEffect(source, effect)` | Skill Feature | 정수·무기 런타임 트리거 효과 주입 |
| `SpawnManager.OnEnemyDied` | `Assets/Scripts/Shared/Managers/SpawnManager.cs:801` | 이미 엘리트 정수 드랍 TODO 훅(`812-818`) 존재. 드랍 판정 추가 지점 |
| `SpawnManager.deathQueue` + `FlushDeathQueue` | 같은 파일 `886-909` | float 배열 배치 전송. 신규 `EventCode_DropSpawnBatch`를 병렬로 추가 |
| `PoolManager.Get/Return` | `Assets/Scripts/Shared/Managers/PoolManager.cs:62-94` | 모든 픽업 프리팹 풀링 |
| `LevelUpPanel` + `SkillCardUI` | UI/Presentation | `isChaos` 파라미터 패턴처럼 `isStatBoost` 파라미터 확장으로 능력치 선택지 재사용 |
| `SkillManager.GenerateChaosChoices` | `SkillManager.cs:557` | 능력치 부스트 선택지 생성 시 같은 패턴 차용 |
| `BossChaosApplicator` | `Features/Boss/Adapter/` | 혼돈 스킬 등급 적용 시 `DetermineBossChaosSkill`에서 등급 가중치만 추가 |

---

## 진행 상태 스냅샷 (2026-04-23 세션 종료 시점)

- **완료**: P0.1, P0.2, Phase 0, Phase 1, Phase 2, Phase 3 (정수), Phase 4 (무기 — W2 포트 추출 + Data SO + Pickup + Inventory + DropSpawner 연동)
- **다음 진입**: **Phase 5 — 능력치(StatBoost)**. W2 포트는 이미 추출돼 있어 별도 선행 작업 없음.
- **미진행**: Phase 5 (StatBoost), Phase 6 (Quest), Phase 7 (혼돈 등급)

### 다음 세션 진입 시 반드시 확인

1. 유저 Unity 작업이 아직 완료 안 됐을 수 있음 — WeaponData SO 5~8 종 생성 / WeaponDatabase SO 생성 + `GameManager.weaponDatabase` 에 할당 / `Weapon.prefab` (WeaponPickup + Collider2D + Rigidbody2D + SpriteRenderer) 작성 + `DropSpawner.weaponPrefab` 할당 / `PlayerWeaponInventory` 컴포넌트를 Player 프리팹 자식에 부착 (자체 PhotonView 필요). 셋업 완료 여부를 먼저 유저에게 확인.
2. 플레이 테스트: 적 처치 → 무기 드랍 → Space 로 획득 → HUD 는 아직 없으므로 DebugOverlay 로 modifier 수 / runtime effect 수 증가 확인. 4 슬롯 가득 찬 상태에서 조합 레시피 매칭되는 무기 픽업 시 조합 성립 검증.

### Phase 3 구현 후 결정된 규약 (Phase 4+ 참조)

- **source 슬롯 네이밍**: `essence_{type}_{slotIndex}` — SkillTriggerSystem.AddRuntimeEffect 덮어쓰기 회피. 무기도 동일 패턴 `weapon_{id}` 또는 `weapon_{id}_{slot}` 권장.
- **Stack2 시너지 필드**: EssenceData.injectedEffectsStack2 선택 필드 → 무기도 중복 장착 허용 시 동일 패턴 적용 고려.
- **TriggerContext.source**: FireTrigger 시 runtime 효과별 source 주입. 핸들러가 "같은 source 갱신 / 다른 source 공존" 분기에 사용.
- **ApplyDoTHandler / ApplySlowHandler 중첩 지원 완료**: DoTEffect 다중 인스턴스, EnemyMovement.slowStack 곱셈 스택.
- **DamageNearbyHandler 신규**: 반경 내 N마리 고정 데미지. `primary=반경, secondary=수, tertiary=데미지`.
- **SkillTriggerSystem 모든 스킬에 무조건 부착**: SkillManager.CreateSkillSlot 에서 triggerEffects 유무와 무관하게 AddComponent.
- **OnHit/OnKill 전 스킬 일관화**: Projectile/AreaZone/PlacedTurret/OrbitalObject 모두 로컬 소유자 경로에서 FireTrigger 호출. OnExpire/OnInterval/OnPlayerHit 는 미구현 (용도 확정 시 추가 예정).

### Phase 3 에서 남은 기술 부채 (별도 티켓)

- **W2**: ✅ 완료 (Phase 4 착수 시점 선행 처리) — `IRuntimeEffectSink` 포트를 `Shared/Domain/Interfaces/` 에 추출. `SkillTriggerSystem` 이 구현 선언, `PlayerEssenceInventory` / `PlayerWeaponInventory` 는 포트만 의존.
- **W3**: 시너지 로직 테이블화 — `EssenceResolver.Resolve(equipped, db) → (source→effects)[]` 순수 함수로 분리. 3스택/조합 확장 대비.
- **I1**: `"__legacy__"` 상수 Shared 승격 — ApplyDoTHandler/ApplySlowHandler/EnemyMovement 3곳 중복.
- **설계 문서 동기화**: `docs/game-design/essence.md § 3.2` 의 "번개 → Chain" 표기를 실제 구현인 "DamageNearby" 로 정정 필요 + 중첩/시너지 규약 추가.
- **W4 (신규 Phase 4 부채)**: `IPlayerStatsMutator` / `ISkillRegistry` 포트 추출 — Inventory 가 Character.Adapter.PlayerStats / Skill.Adapter.SkillManager 를 직접 참조 중. Phase 5 착수 전 처리 권장. architecture-guardian 감사 결과 참조.

### Phase 4 리팩터 (추가 라운드) — 2026-04-23

1. **3-op ModifierOp 전환** — `ModifierOp.Multiply` 를 `Multiplicative` 로 리네임 + `PercentBonus` 추가. 공식 변경:
   ```
   Final = (Base + ΣAdd) × (1 + ΣPercentBonus) × ΠMultiplicative
   ```
   의도: 무기/패시브의 "+10%" 같은 값이 가산 스택하는 직관적 동작(`PercentBonus`), 혼돈 유리대포의 "HP ×0.5" 같은 "언제나 n 배" 의도 보존(`Multiplicative`) 을 분리. 모든 기존 혼돈 호출처는 `Multiplicative` 로 매핑 (`chaos_attack` / `chaos_cdr` / `chaos_maxhp`).
2. **Per-entry isUnique** — `WeaponStatEntry.isUnique` + 신규 `WeaponTriggerEntry { SkillTriggerEffect effect; bool isUnique }`. WeaponData.triggerEffects 를 triggerEntries 로 교체.
3. **Source 네이밍 확장** —
   - unique: `weapon_{id}_u_e{entryIdx}` (슬롯 무관, 중복 장착해도 1회분)
   - non-unique: `weapon_{id}_s{slotUid}_e{entryIdx}` (슬롯별 독립)
4. **slotUid 결정성** — 호스트가 할당 후 RPC (`RPC_EquipWeapon(id, slotUid)`, `RPC_CombineWeapon(consumed, result, resultSlotUid)`) 에 실어 전달. 모든 클라가 동일 값 사용. 기존 static counter 방식에서 호스트 마이그레이션 후 클라 간 어긋남 발생 가능성을 차단.
5. **RPC 이름 변경** — `RPC_Equip` → `RPC_EquipWeapon`, `RPC_Combine` → `RPC_CombineWeapon`. Essence 의 `RPC_Equip(int)` 와 이름 충돌 리스크 사전 차단 (같은 PhotonView 공유 설정으로 이행 시에도 안전).

---

## Priority 0 — 선행 버그픽스 (드랍 시스템 착수 전 반드시 해결)

### P0.1 — 게임 시작 시 경험치 UI 이미 차있는 현상 (최우선) ✅ 완료

- [x] 원인 식별
- [x] 수정
- [x] 회귀 테스트

**현상**: 런 시작 직후 HUD 경험치 바가 0이 아닌 값으로 표시됨.

**조사 지점**:
- `Features/Progression/Adapter/Levelupmanager.cs`, `Shared/Managers/GameManager.AddExp`, 레벨별 필요 XP 계산, `InGameHUD` 의 XP 바인딩
- 레이스 컨디션 후보: HUD가 먼저 바인딩된 값을 표시한 뒤 `ExpService`가 0으로 리셋되지 않았을 가능성
- 테스트/디버그 경로에서 XP 선지급 코드가 남아있는지 (`TestManager`, `Phase2TestEntry`)
- 씬 재진입 시 상태 누수 (정적 상태/싱글턴)

**조치**: 원인 식별 후 수정. 드랍 시스템 어떤 Phase보다 먼저.

### P0.2 — 경험치 오브 동시 존재 수량 제한 ✅ 완료

- [x] `GameplayConfig.maxActiveExpOrbs` 추가
- [x] 상한 도달 시 정책 (**드랍 생략** 으로 결정 — 병합 아님)
- [x] SpawnManager.activeOrbs FIFO 추적 + OnExpOrbReturned 알림

**현상**: 적을 대량으로 죽여 오브가 누적되면 프레임 드랍.

**조치 설계**:
- `GameplayConfig.maxActiveExpOrbs` (기본 200 등) 필드 추가
- `SpawnManager` 가 현재 활성 오브 수 추적
- 상한 도달 시 정책: (A) 가장 오래된 오브를 플레이어에게 즉시 자석 흡수시켜 회수, (B) 여러 오브를 하나의 "대형 오브(합산 XP)"로 병합, (C) 단순 드랍 생략 후 XP를 근접 플레이어에게 직접 누적
- 권장: (B) 병합 — 시각적 손실 없음 + 픽업 카운트 감소. `ExperienceOrb.Initialize(pos, exp)` 는 이미 합산 XP를 받도록 설계되어 있으므로 호환
- Phase 1 (ExpOrb 리팩터링) 에 함께 구현 가능하지만 버그 리스크 높아 선행

**검증**: 적 500 마리 강제 스폰 후 프레임 유지, 오브 수 <= 상한, 경험치 총량 일치.

---

## Phase 0 — 공통 인프라 ✅ 완료

- [x] Rarity / RarityWeightedRoller / RarityPoolChoiceGenerator
- [x] IPickup / PickupType / PickupItemBase
- [x] EnemyDropTable SO (Shared/Data 승격) + DropSpawner
- [x] EventCode_DropSpawnBatch (= 13)
- [x] GameplayConfig / EnemyData 필드 연결

**목표**: 모든 후속 Phase가 공유할 타입/베이스/네트워크 채널을 하나로 통일.

**신규 파일**
- `Assets/Scripts/Shared/Domain/ValueObjects/Rarity.cs` — `enum Rarity { Common, Rare, Epic, Legendary }` (순수 C#, UnityEngine 금지)
- `Assets/Scripts/Shared/Domain/ValueObjects/RarityWeightedRoller.cs` — 순수 C# 정적 유틸. `Rarity Roll(float[] weights, System.Random rng)` — 등급 하나를 먼저 결정.
- `Assets/Scripts/Shared/Domain/ChoiceGeneration/RarityPoolChoiceGenerator.cs` — **공통 등급 선정기**. `T[] PickChoices<T>(IEnumerable<T> pool, Func<T,Rarity> rarityOf, float[] weights, int count)`. "먼저 Rarity 롤 → 해당 등급 풀에서 count장 중복 없이 샘플" 규칙. 혼돈 스킬 선택지, 능력치 부스트 선택지 모두 이 한 경로로 통일 → 카드 3장이 항상 같은 등급.
- `Assets/Scripts/Features/Pickup/Domain/IPickup.cs` — `string ItemId`, `PickupType Type`, `Rarity Rarity`, `void OnPickedUp(PlayerStub picker)` 인터페이스
- `Assets/Scripts/Features/Pickup/Domain/PickupType.cs` — `enum PickupType { ExpOrb, Magnet, Potion, Essence, Weapon }` (StatBoost 드랍 대상 아님 — 제외)
- `Assets/Scripts/Features/Pickup/Adapter/PickupItemBase.cs` — 추상 `MonoBehaviour, IPoolable, IPickup`. 자석 흡수 로직(`ExperienceOrb` 패턴 이식) + `GameState.Playing/BossFight` 체크 + 호스트 권위 픽업. 파생 클래스는 `OnPickedUpByPlayer(PlayerStub)`만 구현.
- `Assets/Scripts/Features/Pickup/Adapter/Data/EnemyDropTable.cs` — SO. 필드: `essenceChance`(엘리트 전용), `weaponChance`, `magnetChance`, `potionChance` + 각 등급 가중치 배열. **StatBoost는 드랍 아님이라 제외.** `CreateAssetMenu("SwDreams/Data/EnemyDropTable")`.
- `Assets/Scripts/Features/Pickup/Adapter/DropSpawner.cs` — 호스트 전용. `TrySpawnDropsForEnemy(Enemy, bool isElite)` → DropTable 룰 → 배치 큐에 적재. 엘리트 전용 정수 분기 포함.
- `Assets/Scripts/Shared/Network/DropSpawnBatch.cs` — `EventCode_DropSpawnBatch = 13` 상수 + (prefabId, x, y, rarity, dataId) 직렬화 포맷

**수정 파일**
- `Assets/Scripts/Shared/Data/GameplayConfig.cs` — 기본 드랍 확률, 등급별 가중치 필드 추가
- `Assets/Scripts/Features/Enemy/Adapter/Data/EnemyData.cs` — `EnemyDropTable dropTable;` 필드(기존 `essenceDropChance` 교체)

**검증**: 빈 프리팹 하나를 PickupItemBase 상속 테스트용으로 만들어 자석 흡수/픽업만 동작 확인. 기능 없음.

---

## Phase 1 — 기존 ExperienceOrb 리팩터링 ✅ 완료

- [x] ExperienceOrb → PickupItemBase 상속 전환
- [x] SpawnManager.SpawnExpOrb 경로 유지 (XP orb 는 100% 드랍이라 DropSpawner 경로 불필요 — SpawnManager 전담 유지)
- [x] 회귀 테스트(XP 획득 / 레벨업) 통과

**목표**: 공통 PickupItemBase에 기존 오브 이식 후 다른 픽업도 같은 베이스로 만들 수 있는지 증명.

**수정 파일**
- `ExperienceOrb.cs` — `PickupItemBase` 상속으로 전환. 자석/호스트 체크 로직 제거(베이스에서 상속), `OnPickedUpByPlayer`만 `GameManager.AddExp(expValue)` 호출.
- `SpawnManager.SpawnExpOrb` (`784`) — 신규 DropSpawner 경로 통해 스폰하도록 변경 (또는 기존 직접 호출은 유지하되 픽업 로직은 베이스에 위임).

**네트워크**: 현재 경험치 오브는 모든 클라에서 로컬 생성(RPC 없음). 그대로 유지.

**검증**: 기존 플레이 테스트에서 경험치 획득/레벨업이 회귀 없이 동작.

---

## Phase 2 — 자석(Magnet) / 물약(Potion) 아이템 ✅ 완료

- [x] MagnetPickup / PotionPickup (PickupItemData SO 는 생략 — SerializeField 로 충분)
- [x] 프리팹 2종 (Magnet.prefab / Potion.prefab)
- [x] SpawnManager.OnEnemyDied → DropSpawner.TrySpawnDrops 연동 + 기존 essenceDropChance 로그 제거
- [x] EventCode_DropSpawnBatch 송수신
- [x] IHealable 포트 추가 (Pickup → Character 경계)
- [x] 자석 RPC 1프레임 지연으로 RPC/RaiseEvent 채널 순서 보장
- [x] HostMigrationHandler 에 DropSpawner.ResetForMigration 연동
- [x] 자석이 ExpOrb 만 끌어오도록 필터 (연쇄 방지)
- [x] 드랍 위치 scatter (GameplayConfig.dropScatterRadius)

**목표**: 가장 단순한 드랍 2종으로 DropSpawner → 배치 RPC → 픽업 전체 파이프라인 검증.

**신규 파일**
- `Features/Pickup/Adapter/MagnetPickup.cs` — 픽업 시 모든 클라에 `RPC_ActivateMagnet` 브로드캐스트. 맵의 모든 ExperienceOrb를 해당 플레이어에게 즉시 흡수.
- `Features/Pickup/Adapter/PotionPickup.cs` — 픽업 시 호스트가 `PlayerHealth.Heal(baseHeal × HealMultiplier)` 호출, 이벤트로 시각 동기화. 획득자만 회복.
- `Features/Pickup/Adapter/Data/PickupItemData.cs` — SO. 자석/물약 공통 수치(heal amount, 발동 시각 효과 프리팹 등).
- 프리팹: `Assets/Resources/Prefabs/Pickup/Magnet.prefab`, `Potion.prefab`

**수정 파일**
- `SpawnManager.OnEnemyDied` — `DropSpawner.TrySpawnDropsForEnemy` 호출. 기존 엘리트 TODO 코드(`812-818`) 제거.
- `SpawnManager.FlushDeathQueue` 옆에 `FlushDropQueue` 추가 → `EventCode_DropSpawnBatch` 송신.

**네트워크**: 드랍은 로컬 생성으로 통일(월드 오브젝트 = 모든 클라에서 동일 좌표). 호스트만 픽업 권위.

**검증**: 적 사망 시 확률 드랍 → 자석/물약 주웠을 때 효과 발동 + 모든 플레이어 화면에서 일관된 상태.

---

## Phase 3 — 정수(Essence) 시스템 ✅ 대부분 완료

- [x] EssenceType (Ice/Fire/Lightning, Domain enum)
- [x] EssenceData SO + EssenceDatabase SO (GameManager.EssenceDB 단일 소유)
- [x] EssencePickup + PlayerEssenceInventory (최대 2슬롯, AllBuffered RPC)
- [x] AddRuntimeEffect 주입 (source 슬롯 네이밍 + Stack2 시너지)
- [x] 상호작용 기반 획득 UX (Space 키 + CanBePickedUpBy 2슬롯 가득 시 차단)
- [x] OnHit/OnKill 트리거 전 스킬 일관화 (Projectile/AreaZone/PlacedTurret/OrbitalObject)
- [x] DamageNearbyHandler (번개 정수용 신규 action type)
- [x] DebugOverlay — Essence + T:{base}+{runtime} H:{onHit} 표시
- [ ] EssenceSlotsUI HUD (향후 작업 — Phase 4 와 병행 or 후속)
- [ ] EssenceCombo VO (조합 히든 효과 — 설계서 TBD 상태, 수치 확정 후 착수)

**목표**: 엘리트 드랍 + 속성 효과(불/얼음/번개) 주입 + 2개 슬롯 + 조합 히든 효과.

**신규 파일**
- `Features/Essence/Domain/EssenceType.cs` — `enum EssenceType { Ice, Fire, Lightning }` (순수 C#)
- `Features/Essence/Domain/EssenceCombo.cs` — 조합 효과 VO (얼음+불 → ?, 얼음+번개 → 치명타 전이, 불+번개 → ?)
- `Features/Essence/Adapter/Data/EssenceData.cs` — SO. `EssenceType type, SkillTriggerEffect[] injectedEffects`
- `Features/Essence/Adapter/EssencePickup.cs` — `PickupItemBase` 상속
- `Features/Essence/Adapter/PlayerEssenceInventory.cs` — `MonoBehaviour`, 플레이어 자식으로 붙음. 최대 2개 슬롯. 변경 시 `SkillTriggerSystem.AddRuntimeEffect("essence_{id}", effect)` / `RemoveRuntimeEffect` 호출. 2개 보유 시 조합 효과도 등록(`essence_combo_{iceFire}` source).
- `Features/Essence/Presentation/EssenceSlotsUI.cs` — HUD에 현재 보유 정수 2슬롯 표시

**수정 파일**
- `EnemyDropTable` — 엘리트 전용 essence drop 섹션 (speed/slow/range 중 랜덤 1종 또는 테이블 가중치)
- `InGameHUD.prefab` — EssenceSlotsUI 배치

**속성 효과 주입 방식 (확정)**: `docs/systems/trigger-effects.md § 5` 의 `SkillTriggerSystem.AddRuntimeEffect(source, SkillTriggerEffect)` 규약 그대로 사용. 재정의 금지. 정수 장착 시 `OnHit → ApplyDoT/ApplySlow/Chain` 를 런타임 트리거로 주입, 해제 시 `RemoveRuntimeEffects("essence_{id}")` 또는 `RemoveByPrefix("essence_")`. 기존 스킬의 TriggerEffect 목록에 추가만 되는 방식이라 스킬 SO는 변경 없음.

**검증**: 엘리트 적 처치 → 정수 드랍 → 주우면 SkillTriggerSystem 에 런타임 효과 주입 → 기존 스킬 발사에 자동 적용(예: 얼음이면 투사체에 슬로우 DoT). 2개 보유 시 3번째 드랍은 회색/픽업 불가.

---

## Phase 4 — 무기(Weapon) 시스템 ✅ 코드 구현 완료 (유저 Unity 배선 대기)

- [x] **W2 포트 추출**: `IRuntimeEffectSink` (Shared/Domain/Interfaces). `SkillTriggerSystem` 가 구현. `PlayerEssenceInventory` 도 포트 의존으로 전환 (SkillTriggerSystem 직접 참조 제거).
- [x] `WeaponStatEntry` / `WeaponCombineRecipe` VO (Features/Weapon/Domain, 순수 C#)
- [x] `WeaponData` SO + `WeaponDatabase` SO (Features/Weapon/Adapter/Data)
- [x] `WeaponPickup` (PickupItemBase 상속, RequiresInteraction + PromptExtraInfo 조합 프리뷰)
- [x] `PlayerWeaponInventory` (4슬롯, AllBuffered `RPC_Equip`/`RPC_Combine`, 조합 매처)
- [x] `DropSpawner` Weapon 분기 — `WeaponDatabase.All` 인덱스를 `dataIdHash` 로 전송
- [x] `GameManager.WeaponDB` 노출
- [ ] **WeaponSlotsUI / 조합 프리뷰 HUD — Phase 5 와 병행 착수 예정** (현재는 `DebugOverlay` 로 modifier/runtime effect 수 관찰)
- [ ] **유저 Unity 배선** (아래 체크리스트)

### 세션 진입점 체크리스트 (다음 세션 시작 시 반드시 확인)

**Phase 3 완료 상태라 다음 세션은 Phase 4 구현 착수가 최우선.** 이전 세션에서 설계 확인 + 기존 인프라 점검까지 끝낸 상태.

1. 최근 커밋 확인: `git log --oneline -5` — 최상단 `a723d9f79 feat: 드랍 시스템 Phase 3` 가 있으면 정상.
2. 이 문서의 "Phase 3 구현 후 결정된 규약" 섹션을 먼저 숙지 — 무기도 동일 규약 따름.
3. 설계서: [docs/game-design/weapon.md](../game-design/weapon.md) § 5 참조. StatModifier 편집 가능한 Serializable 구조 필요.

### 확인된 사실 (이전 세션 탐색 결과)

- **StatType enum** 에 `CritChance`, `LifeSteal` 이미 정의됨 (StatType.cs). 무기/정수 시스템용 예약. 재정의 금지.
- **StatModifier** 는 `readonly struct` — Inspector 편집 불가. WeaponData SO 에 별도 Serializable 구조(`WeaponStatEntry { StatType, ModifierOp, float }`) 도입 필요. 런타임에 `new StatModifier("weapon_{id}", ...)` 로 변환.
- **PlayerStats.AddModifier(StatModifier)** / **RemoveModifiersBySource(string)** / **RemoveModifiersByPrefix("weapon_")** 모두 기존 API 존재. 그대로 사용.
- **SkillTriggerSystem.AddRuntimeEffect / RemoveByPrefix("weapon_")** 도 그대로 사용.
- **PickupItemBase.PromptExtraInfo** virtual 훅이 Phase 3 에서 이미 준비됨 — WeaponPickup.PromptExtraInfo 에 조합 결과명 반환하면 InteractionPromptUI 가 자동 표시.

### Phase 4 구현 단계 (권장 순서)

1. **WeaponCombineRecipe VO** (Features/Weapon/Domain, 순수 C#) — `WeaponData[] inputs, WeaponData output` Serializable 구조
2. **WeaponData SO** (Features/Weapon/Adapter/Data) — weaponId, displayName, sprite, rarity, statEntries[], triggerEffects[], combineRecipe, skillTypeAffinity
3. **WeaponDatabase SO** (Features/Weapon/Adapter/Data) — weaponId → WeaponData lookup. GameManager.WeaponDB 로 SSOT 노출
4. **WeaponPickup** (Features/Weapon/Adapter) — PickupItemBase 상속. RequiresInteraction=true. PromptActionLabel="무기 획득"/"조합". PromptExtraInfo=조합 결과명
5. **PlayerWeaponInventory** (Features/Weapon/Adapter) — MonoBehaviourPun 4슬롯. `TryAddOrCombine(WeaponData)`, RPC_EquipWeapon / RPC_CombineWeapon (AllBuffered)
6. **DropSpawner Weapon 분기** — SpawnPickupLocal 에 Essence 분기와 유사하게 WeaponPickup.InitializeWeapon 호출. dataIdHash 자리에 weaponId hash 전달
7. **WeaponDropTable 필드** — 기존 EnemyDropTable.weaponChance + rarity 가중치는 Phase 0 에서 준비됨
8. **감사**: architecture-guardian (W2 포트 추출 여부) + photon-sync-auditor (조합 RPC 경로)

### W2 포트 추출 (선행 권고)

Phase 4 구현 전에 `IRuntimeEffectSink { AddRuntimeEffect, RemoveRuntimeEffects, RemoveByPrefix }` 포트를 `Shared/Domain/Interfaces/` 에 만들고 SkillTriggerSystem 이 구현 선언. Essence/Weapon 둘 다 포트만 의존하도록. Phase 4 가 같은 결합 문제를 재발하기 전에 정리하는 게 비용 대비 효과 큼. 설계 문서 없이 코드 수정만 30분 예상.

### 유저 Unity 작업 (Phase 4 구현 후) — 아래 순서로 진행

1. **WeaponData SO 5~8종 생성**: `Assets → Create → SwDreams/Data/WeaponData`. 각 SO 에 `weaponId` (고유), `rarity`, `statEntries` (StatType/ModifierOp/value), `triggerEffects` (optional, OnHit/OnKill 등), `combineRecipe` (optional — 결과 무기 SO 가 "나를 만들려면 이 재료들"로 기입).
2. **WeaponDatabase SO 생성**: `Assets → Create → SwDreams/Data/WeaponDatabase`. `weapons` 리스트에 위 1의 SO 전부 등록. 리스트 순서가 네트워크 인덱스 기반이라 빌드 간 일관 유지.
3. **GameManager Inspector** 에 `weaponDatabase` 할당.
4. **Weapon.prefab 작성**: `WeaponPickup` 스크립트 + Collider2D(isTrigger) + Rigidbody2D(Kinematic) + SpriteRenderer.
5. **DropSpawner Inspector** 에 `weaponPrefab` 할당. `EnemyDropTable.weaponChance` 를 적 SO 별로 0.01~0.05 수준 조정.
6. **Player 프리팹에 PlayerWeaponInventory 부착**: 자식 GameObject 로 배치 + 자체 PhotonView 컴포넌트 필수 (Essence 와 동일 패턴). Observed Components 비움 (RPC 전용).
7. **Player 프리팹에 PlayerPickupInteractor** 가 이미 있는지 확인 — 없으면 Essence 때 같이 붙였어야 함. 무기도 같은 interactor 재사용.

### 네트워크 알려진 제약

- **Host-migration ownership**: 새 호스트가 타 플레이어의 `PlayerWeaponInventory.photonView` 를 통해 `AllBuffered` RPC 를 쏠 때 Owner 가 아니라 master 자격으로 송신. PUN 2 기본 정책에서 허용되지만, OwnershipOption 을 Fixed 가 아닌 값으로 바꾸면 권한 이슈 가능 — 현재는 Essence 와 동일 패턴이라 재현 사례 없음. 검증 시나리오는 `drop-system-roadmap.md` Phase 4 audit 결과 참조.
- **AllBuffered 재생 순서**: 무기 RPC 가 스킬 획득 RPC 보다 먼저 도착하면 `InjectTriggers` 시점에 스킬이 비어 있을 수 있음 — 이 케이스는 `PlayerWeaponInventory.HandleSkillAdded` 가 나중 스킬 추가 시 기존 장착 무기의 triggerEffects 를 재주입하므로 자동 복구.

**목표**: 4슬롯 장비 + 등급 + 조합 + LoL 아이템식 스탯/트리거 부여.

**신규 파일**
- `Features/Weapon/Domain/WeaponCombineRecipe.cs` — 조합 레시피 VO (input weapon ids → output weapon id)
- `Features/Weapon/Adapter/Data/WeaponData.cs` — SO. `string weaponId, Rarity rarity, StatModifier[] statModifiers, SkillTriggerEffect[] triggerEffects, WeaponCombineRecipe recipe, SkillType[] skillTypeAffinity`
- `Features/Weapon/Adapter/Data/WeaponDatabase.cs` — SO 루트 (SkillDatabase 대응)
- `Features/Weapon/Adapter/WeaponPickup.cs` — `PickupItemBase` 상속. 근접 시 조합 결과 미리보기 UI 트리거.
- `Features/Weapon/Adapter/PlayerWeaponInventory.cs` — 플레이어 자식. 최대 4슬롯. 장착/교체/조합 로직. 장착 시 `PlayerStats.AddModifier(source="weapon_{id}")` 및 `SkillTriggerSystem.AddRuntimeEffect("weapon_{id}")`. 교체/조합 시 source prefix로 일괄 제거.
- `Features/Weapon/Presentation/WeaponSlotsUI.cs` — HUD 4슬롯 + 등급 색상 테두리
- `Features/Weapon/Presentation/WeaponCombinePreview.cs` — 근접 시 조합 결과 프리뷰 팝업 (Frame)

**수정 파일**
- `EnemyDropTable` — weapon drop chance (매우 낮음)
- `InGameHUD.prefab` — WeaponSlotsUI

**검증**: 드랍 → 슬롯 장착 → 스탯/트리거 즉시 반영. 슬롯 꽉 찬 상태에서 조합 가능한 무기 픽업 시 조합 실행 → 재료 제거 + 결과 장착.

---

## Phase 5 — 능력치(Stat Boost) 시스템

- [ ] StatBoostData SO / StatBoostDatabase / StatBoostManager
- [ ] StatBoostChoiceService (공통 선정기 재사용)
- [ ] LevelUpManager 만렙 분기 + LevelUpPanel panelKind 확장
- [ ] StatBoostCardUI

**중요**: StatBoost 는 **월드 드랍 아님**. 진입구는 두 개뿐:
1. **만렙 후 레벨업** — 현재 Lv = Max 상태에서 XP 게이지가 다시 차 레벨업 이벤트 발생 시, 스킬 선택지 대신 StatBoost 선택지.
2. **퀘스트 완료 보상** — Phase 6에서 호출.

**목표**: 두 입구 모두 4등급 공통 선정기로 3장(동일 등급) 뽑아 선택지 제공.

**신규 파일**
- `Features/StatBoost/Adapter/Data/StatBoostData.cs` — SO. `string boostId, Rarity rarity, StatType statType, ModifierOp op, float value, Sprite icon`
- `Features/StatBoost/Adapter/StatBoostDatabase.cs` — SO 루트. 등급별 풀 접근자.
- `Features/StatBoost/Adapter/StatBoostManager.cs` — 플레이어 자식. `ApplyChoice(StatBoostData)` → `PlayerStats.AddModifier(source="stat_{boostId}", ...)`.
- `Features/StatBoost/Adapter/StatBoostChoiceService.cs` — Phase 0의 `RarityPoolChoiceGenerator` 를 호출해 3장 생성. **혼돈 스킬과 동일한 공통 경로**.
- `Features/StatBoost/Presentation/StatBoostCardUI.cs` — SkillCardUI 와 같은 레이아웃 재사용 (혹은 SkillCardUI 가 StatBoost 도 렌더링하도록 확장 — 등급 색상은 공통)

**수정 파일**
- `LevelUpManager` (`Features/Progression/Adapter/Levelupmanager.cs`) — 만렙 상태 분기 추가. 기존 `isChaosLevel` 분기 옆에 `isMaxLevelLevelUp` 추가. 만렙 판정 기준 `GameplayConfig.maxPlayerLevel` 신설.
- `LevelUpPanel` — `Setup(choices, panelKind)` 시그니처로 확장. `panelKind = Skill / Chaos / StatBoost`. 타이틀 텍스트만 다름.
- `GameplayConfig` — 능력치 등급별 가중치(기본 60/25/12/3), 만렙 설정.

**검증**: 만렙 도달 후 XP 추가로 먹으면 StatBoost 패널 뜸. 카드 3장이 **모두 같은 등급**. 선택 시 PlayerStats에 즉시 반영, HUD 스탯 값 변화 확인. 동일 boostId 중복 획득 허용 여부는 밸런스 판단 (기본: 허용, 누적).

---

## Phase 6 — 퀘스트(Quest) 시스템

- [ ] QuestType / QuestState / QuestData SO
- [ ] QuestZone 상태 머신 + QuestBarrier
- [ ] QuestRewardDispatcher → StatBoost 선택지
- [ ] QuestProgressUI
- [ ] 맵 배치 (WFC 또는 사전 배치)

**목표**: 맵 거점 진입형 부가 목표 4유형, 보상은 StatBoost 선택지(Phase 5 재사용).

**신규 파일**
- `Features/Quest/Domain/QuestType.cs` — `enum QuestType { KillTarget, KillInTime, DodgeFalling, Defend }`
- `Features/Quest/Domain/QuestState.cs` — `enum QuestState { Idle, Waiting, InProgress, Completed, Failed }`
- `Features/Quest/Adapter/Data/QuestData.cs` — SO. 목표/제한시간/대기시간/봉쇄 적 데이터/보상 등급 가중치
- `Features/Quest/Adapter/QuestZone.cs` — 맵 거점 컴포넌트. 트리거 진입 감지 → 모든 플레이어 반경 내 + 대기 시간 → 시작. 상태 머신.
- `Features/Quest/Adapter/QuestBarrier.cs` — 격리 몹 스폰/관리. 기존 SpawnManager 재사용.
- `Features/Quest/Adapter/QuestRewardDispatcher.cs` — 완료 시 참여 플레이어에게 StatBoost 선택지 트리거
- `Features/Quest/Presentation/QuestProgressUI.cs` — 현재 진행 중인 퀘스트 HUD 표시

**수정 파일**
- `WFC` 또는 맵 프리팹 — 퀘스트 거점 사전 배치(또는 SpawnManager에 런타임 랜덤 배치 훅 추가)
- `GameplayConfig` — 거점 개수/최소 간격 등

**네트워크**: QuestZone은 호스트 권위 상태 머신 → `RPC_UpdateQuestState`로 동기화. 격리 몹은 기존 SpawnManager 경로 사용.

**검증**: 맵에 거점 배치 → 모든 플레이어 반경 진입 → 대기 → 시작 → 완료 → 보상 카드 3장.

---

## Phase 7 — 혼돈 스킬 등급 적용

- [ ] SkillData.rarity 필드 + 혼돈 SO 19개 등급 지정
- [ ] SkillManager.GenerateChaosChoices → RarityPoolChoiceGenerator 전환
- [ ] SkillCardUI 등급 색상/테두리
- [ ] BossChaosApplicator 등급 가중치(선택)

**목표**: 기존 혼돈 스킬 플로우에 `Rarity` 필드 추가 + Phase 0 공통 선정기로 전환해 카드 3장 동일 등급 유지.

**수정 파일**
- `Features/Skill/Adapter/Data/SkillData.cs` — `public Rarity rarity;` 필드 추가 (Active/Passive/Chaos 공통). 기본값 Common.
- 모든 혼돈 스킬 SO 19개 — 등급 재지정 (디자이너 작업)
- `SkillManager.GenerateChaosChoices` (`SkillManager.cs:557`) — Phase 0 `RarityPoolChoiceGenerator.PickChoices<ChaosSkillData>` 로 교체. 기존 "랜덤 3개 셔플" 제거.
- `SkillCardUI` — 등급별 색상/테두리 (타입 색상과 병행 표시, 등급 우선)
- `BossChaosApplicator.DetermineBossChaosSkill` — 미선택 풀에서 등급 가중치 적용 (선택). 등급별 보스 강도 차등 여부는 별도 기획.

**능력치와 완전히 같은 공통기 경유**: Phase 5 `StatBoostChoiceService` 와 이 Phase 의 혼돈 선정기가 `RarityPoolChoiceGenerator` 한 메서드를 공유하므로, "카드 3장이 항상 같은 등급" 규칙은 한 곳에서만 관리된다.

**검증**: 레벨 10/20/30 선택지 카드 3장이 모두 같은 등급. 등급 분포 변화(일반 60% / 희귀 25% / 영웅 12% / 전설 3% 기본). 카드 UI에 등급 시각화.

---

## 공통 의사결정 기록

1. **드랍 오브젝트 네트워크 모델**: 로컬 생성(모든 클라 동일 좌표) + 호스트 권위 픽업 판정. 기존 ExperienceOrb 패턴 일관 유지. `PhotonNetwork.Instantiate` 사용 안 함.
2. **Source 접두사 컨벤션** (PlayerStats.modifiers): `passive_ / evolution_ / chaos_ / essence_ / essence_combo_ / weapon_ / stat_ / buff_`. 교체/해제 시 prefix 한 줄 제거로 끝.
3. **SkillTriggerSystem.AddRuntimeEffect source 컨벤션**: `essence_{id} / weapon_{id} / chaos_{id}`. 동일.
4. **SO 생성 메뉴 루트**: `SwDreams/Data/{Essence|Weapon|Quest|StatBoost|EnemyDropTable}`.
5. **배치 이벤트 코드**: 기존 `EnemyDeathBatch=11`, `EnemyRemoveBatch=12` 다음 `DropSpawnBatch=13`.
6. **풀링**: 모든 픽업 프리팹을 `PoolManager.Prewarm`으로 게임 시작 시 warm-up.

---

## 각 Phase 공통 체크리스트

- [ ] 새 SO 설계 시 `docs/templates/system-spec.md` 기반 `.md` 문서 먼저 or 동시
- [ ] Domain 레이어 파일에 `UnityEngine` / `Photon` import 없는지 확인
- [ ] `[PunRPC]`, `RaiseEvent`, `PhotonView` 변경 시 `photon-sync-auditor` 서브에이전트 호출
- [ ] 네트워크 테스트: 호스트 시점 / 클라 시점 / 호스트 마이그레이션 시
- [ ] 만렙·일시정지·보스전 상태에서 각 플로우 동작 확인
- [ ] 기존 회귀: 경험치 획득/레벨업/혼돈 스킬 플로우 정상

---

## 관련 문서

- [docs/game-design/essence.md](../game-design/essence.md)
- [docs/game-design/weapon.md](../game-design/weapon.md)
- [docs/game-design/quest.md](../game-design/quest.md)
- [docs/game-design/stat-boost.md](../game-design/stat-boost.md)
- [docs/game-design/items.md](../game-design/items.md)
- [docs/systems/trigger-effects.md § 5](../systems/trigger-effects.md) — AddRuntimeEffect 규약 (SSOT)
- [docs/systems/network-sync.md](../systems/network-sync.md)
- [docs/architecture/implementation-roadmap.md](implementation-roadmap.md) — 전체 Phase 로드맵
