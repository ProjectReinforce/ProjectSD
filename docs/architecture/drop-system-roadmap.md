# 드랍 시스템 구현 로드맵 — 정수/무기/퀘스트/능력치/기타아이템/혼돈등급

> 2026-04-21 승인본. **2026-04-26 통합 정리**: Phase 0~7 코드 측 핵심은 모두 완료. 본 문서는 이제 **잔여 + paramsByRarity 치트시트 + 결정 규약** 의 보관소 역할만 한다.
>
> **완료 내역 ledger**: [completed-work.md § 드랍 시스템 구현 (Phase 0 ~ 7)](completed-work.md)
> **잔여 작업 (HUD / 유저 Unity 배선 / Quest 핸들러)**: [implementation-roadmap.md § DQ](implementation-roadmap.md)

---

## 진행 상태 요약 (2026-04-26)

| Phase | 상태 | 비고 |
|---|---|---|
| P0.1 / P0.2 선행 버그픽스 | ✅ 완료 | maxActiveExpOrbs=200, FIFO 추적 |
| Phase 0 — 공통 인프라 | ✅ 완료 | Rarity / RarityPoolChoiceGenerator / IPickup / DropSpawner / EventCode=13 |
| Phase 1 — ExpOrb 리팩터링 | ✅ 완료 | PickupItemBase 상속 |
| Phase 2 — 자석 / 물약 | ✅ 완료 | Magnet/Potion + IHealable + dropScatterRadius |
| Phase 3 — 정수(Essence) | ✅ 코드 (HUD 잔여 → DQ2) | 2슬롯 + AddRuntimeEffect + Stack2 시너지 |
| Phase 4 — 무기(Weapon) | ✅ 코드 (HUD/배선 잔여 → DQ3/DQ4) | 4슬롯 + slotUid + 데미지 공식 재설계 |
| Phase 5 — 능력치(StatBoost) | ✅ 코드 (배선 잔여 → DQ5) | 통합 SO + RarityPoolChoiceGenerator |
| Phase 6 — 퀘스트(Quest) | 🟡 코드 핵심 (HUD/3핸들러/맵/배선 → DQ1) | KillTarget MVP + 격리 몹 + 이중카운트 가드 |
| Phase 7 — 혼돈 등급 | ✅ 코드 (등급 재지정 → DQ6) | SkillData.rarity + 공통 선정기 |
| Phase 8 — 혼돈 하드코딩 제거 (W5) | ✅ 8-A/B/B3/C 완료 | Hook Registry + StatWatcher |

상세 완료 항목 = [completed-work.md § 드랍 시스템 구현](completed-work.md).

---

## paramsByRarity 치트시트 (혼돈 SO 입력용)

| 혼돈 | (primary, secondary, tertiary) × 4등급 | 의미 |
|---|---|---|
| GlassCannon | (1.1, 0.5, 0) (1.2, 0.5, 0) (1.3, 0.5, 0) (1.5, 0.5, 0) | ATK 배율 / HP 비율 / - |
| Berserk | (0.9, 0.3, 1.1) (0.8, 0.3, 1.2) (0.7, 0.3, 1.3) (0.5, 0.3, 1.5) | CDR 배율 / HP 임계 / 이속 배율 |
| AccelEngine | (0.1, 600, 0) (0.2, 600, 0) (0.3, 600, 0) (0.5, 600, 0) | 최대 증폭 / 램프 초 / - |
| Unity | (0.03, 0.02, 5) (0.05, 0.025, 5) (0.07, 0.04, 5) (0.10, 0.06, 5) | 1명 근접 보너스 / 추가 인당 / 감지 반경 |
| ChainExplosion | (8, 2, 0) (10, 2, 0) (15, 2, 0) (20, 2, 0) | 폭발 데미지 / 반경 / - (연쇄수는 manager) |
| Gambler | (0, 0, 0) × 4 | 파라미터 미사용 (boolean flag) |

**Gambler rarity bump 분포표**: Common 100% (+1) / Rare 90/10 / Epic 80/20 / Legendary 70/20/10. 상세 [docs/game-design/skills/chaos/gambler.md](../game-design/skills/chaos/gambler.md).

---

## 공통 의사결정 기록 (드랍 시스템 SSOT)

1. **드랍 오브젝트 네트워크 모델**: 로컬 생성(모든 클라 동일 좌표) + 호스트 권위 픽업 판정. `PhotonNetwork.Instantiate` 사용 안 함. (ExperienceOrb 패턴 일관 유지)
2. **Source 접두사 컨벤션** (`PlayerStats.modifiers`): `passive_ / evolution_ / chaos_ / essence_ / essence_combo_ / weapon_ / stat_ / buff_`. 교체/해제 시 prefix 한 줄 제거로 끝.
3. **`SkillTriggerSystem.AddRuntimeEffect` source 컨벤션**: `essence_{id} / weapon_{id} / chaos_{id}`. 동일 source 재호출 = 갱신.
4. **무기 source 네이밍 (Per-entry isUnique)**:
   - unique: `weapon_{id}_u_e{entryIdx}` (슬롯 무관, 중복 장착해도 1회분)
   - non-unique: `weapon_{id}_s{slotUid}_e{entryIdx}` (슬롯별 독립)
5. **slotUid 결정성**: 호스트가 할당 후 RPC (`RPC_EquipWeapon`/`RPC_CombineWeapon`) 에 실어 전달. 모든 클라 동일 값 사용. 호스트 마이그레이션 후 어긋남 차단.
6. **SO 생성 메뉴 루트**: `SwDreams/Data/{Essence|Weapon|Quest|StatBoost|EnemyDropTable}`.
7. **배치 이벤트 코드**: `EnemyDeathBatch=11`, `EnemyRemoveBatch=12`, `DropSpawnBatch=13`, `LoadSceneEvent=15`, `LobbyRefreshEvent=16`.
8. **풀링**: 모든 픽업 프리팹을 `PoolManager.Prewarm` 으로 게임 시작 시 warm-up.
9. **드랍 대상과 비드랍 대상 구분**:
   - 월드 드랍: 경험치 오브 / 자석 / 물약 / 정수(엘리트만) / 무기
   - 월드 드랍 아님: **StatBoost** (만렙 후 레벨업 + 퀘스트 보상) / **퀘스트** (맵 거점) / **혼돈 스킬** (Lv.10/20/30 선택)
10. **등급 선정 규칙**: 선택지 카드 3장은 모두 동일 등급. 먼저 Rarity 가중치 롤 → 해당 등급 풀에서 3장 중복 없이 샘플. 혼돈·StatBoost·(필요 시 무기 조합 미리보기) 모두 `RarityPoolChoiceGenerator` 한 경로 SSOT.

---

## SO 입력값 SSOT (2026-04-24 동기화)

| 항목 | 값 | 출처 |
|---|---|---|
| 일반 적 자석 드랍 | 1% | `EnemyDropTable.magnetChance = 0.01` |
| 일반 적 물약 드랍 | 1% | `EnemyDropTable.potionChance = 0.01` |
| 일반 적 무기 드랍 | 100% | `EnemyDropTable.weaponChance = 1` |
| 일반 적 정수 드랍 | 0% | `EnemyDropTable.essenceChance = 0` |
| 엘리트 정수 드랍 | 100% | `EliteDropTable.essenceChance = 1` |
| 엘리트 무기 드랍 | 0.01% | `EliteDropTable.weaponChance = 0.0001` |
| 무기 등급 가중치 | 60/25/12/3 | `weaponRarityWeights` |
| 경험치 오브 동시 상한 | 200 | `GameplayConfig.maxActiveExpOrbs` |
| 자석 범위 / 속도 | 0.7 / 2 | `GameplayConfig.magnetRange` / `magnetSpeed` |
| 공용 4등급 가중치 | 60/25/12/3 | `GameplayConfig.defaultRarityWeights` |

**참조 SO 경로**:
- 드랍 확률: `Assets/Data/EnemyDropTable.asset`, `Assets/Data/EliteDropTable.asset`
- 경험치 오브 상한·자석 범위·등급 가중치: `Assets/Data/GameplayConfig.asset`
- 정수 속성별 파라미터: `Assets/Data/Pickup/{Fire,Ice,Lightening}EssenceData.asset`

---

## Phase 4 데미지 공식 (참조용)

AttackMultiplier 가 기존에 "단일 배율"로 해석돼 Add 사용 시 폭발적으로 과장되던 문제 해결 (2026-04-24).

```
finalDamage = (skillBase + ΣAdd + skillBase × ΣPercentBonus) × ΠMultiplicative × baseAttackMultiplier
```

**컨벤션**:
- `Add`: 무기 엔트리 `op=Add`. "플랫 +N 데미지" 의도.
- `PercentBonus`: 무기 엔트리 `op=PercentBonus` + AttackMultiplier 패시브 자동 등록. "+N%" 가산.
- `Multiplicative`: 혼돈 스킬 (`chaos_attack`). 향후 무기도 편입 가능 (저주/고유 효과 설계 여지).

**진입점**: `PlayerStats.ApplyAttackTo(skillBase, skillData)` — `SkillExecutor.BuildContext` 가 호출.
**디버그**: `DebugOverlay` 에 ATK 분해 (`+flat, +%, ×mult, base×`) 표시.

---

## 재사용 가능한 기존 자산

신규 작업 시 새 베이스를 만들지 말고 아래 목록을 우선 활용.

| 대상 | 위치 | 용도 |
|---|---|---|
| `ExperienceOrb` | `Features/Progression/Adapter/ExperienceOrb.cs` | `IPoolable` + 자석 흡수 + 호스트 권위 + GameState 체크. **모든 픽업의 베이스 템플릿** |
| `PlayerStats.AddModifier / Recalculate` | `Features/Character/Adapter/PlayerStats.cs` | 무기·능력치 스탯 부여 단일 경로. source 접두사 컨벤션 |
| `SkillTriggerSystem.AddRuntimeEffect(source, effect)` | Skill Feature | 정수·무기 런타임 트리거 효과 주입 (`IRuntimeEffectSink` 포트 의존) |
| `SpawnManager.OnEnemyDied` | `Shared/Managers/SpawnManager.cs` | DropSpawner 드랍 판정 진입점 |
| `SpawnManager.deathQueue` + `FlushDeathQueue` | 같은 파일 | float 배열 배치 전송. 신규 이벤트 코드는 병렬로 추가 |
| `PoolManager.Get/Return` | `Shared/Managers/PoolManager.cs` | 모든 픽업 프리팹 풀링 |
| `LevelUpPanel` + `SkillCardUI` | UI/Presentation | `panelKind` 확장으로 Skill/Chaos/StatBoost 모두 단일 컴포넌트 렌더 |
| `RarityPoolChoiceGenerator` | `Shared/Domain/ChoiceGeneration/` | 등급 롤 → 풀 샘플 공통기. 혼돈·StatBoost SSOT |
| `BossChaosApplicator` | `Features/Boss/Adapter/` | 보스 혼돈 등급 적용 (등급 가중치 추가만 잔여) |

---

## 알려진 기술 부채 (별도 티켓)

- **W3**: 시너지 로직 테이블화 — `EssenceResolver.Resolve(equipped, db) → (source→effects)[]` 순수 함수로 분리. 3스택/조합 확장 대비.
- **I1**: `"__legacy__"` 상수 Shared 승격 — `ApplyDoTHandler` / `ApplySlowHandler` / `EnemyMovement` 3곳 중복.
- **DebugOverlay 릴리스 가드**: `#if UNITY_EDITOR || DEVELOPMENT_BUILD`.
- **`ChoicePanelKind`**: Progression.Domain → Shared 승격 여부 (다른 Feature 가 enum 으로 타입 식별 시작 시).
- **`SkillTriggerEffect` / `StatType` / `ModifierOp`**: Shared/Domain/ValueObjects 승격 (3개 이상 Feature 공유 중).
- **`IChaosHookBus.Vector2`**: Domain 순수성 위반 (Position2D VO 로 분리).
- **`IPlayerTransform`** 포트: `ChaosHandlerContext.playerRoot` 가 구체 Transform.
- **설계 문서 동기화**: [essence.md § 3.2](../game-design/essence.md) 의 "번개 → Chain" → 실제 구현 "DamageNearby" 로 정정 + 중첩/시너지 규약 추가.

---

## 각 Phase 공통 체크리스트 (신규 작업 시)

- [ ] 새 SO 설계 시 `docs/templates/system-spec.md` 기반 `.md` 문서 먼저 or 동시
- [ ] Domain 레이어 파일에 `UnityEngine` / `Photon` import 없는지 확인
- [ ] `[PunRPC]`, `RaiseEvent`, `PhotonView` 변경 시 `photon-sync-auditor` 서브에이전트 호출
- [ ] 네트워크 테스트: 호스트 시점 / 클라 시점 / 호스트 마이그레이션 시
- [ ] 만렙·일시정지·보스전 상태에서 각 플로우 동작 확인
- [ ] 기존 회귀: 경험치 획득/레벨업/혼돈 스킬 플로우 정상

---

## 관련 문서

- [completed-work.md § 드랍 시스템 구현](completed-work.md) — **완료 내역 ledger**
- [implementation-roadmap.md § DQ](implementation-roadmap.md) — **잔여 작업**
- [docs/game-design/essence.md](../game-design/essence.md)
- [docs/game-design/weapon.md](../game-design/weapon.md)
- [docs/game-design/quest.md](../game-design/quest.md)
- [docs/game-design/stat-boost.md](../game-design/stat-boost.md)
- [docs/game-design/items.md](../game-design/items.md)
- [docs/systems/trigger-effects.md § 5](../systems/trigger-effects.md) — AddRuntimeEffect 규약 (SSOT)
- [docs/systems/network-sync.md](../systems/network-sync.md)
