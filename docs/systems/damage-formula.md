# Damage Formula — 데미지 계산 공식 (제안)

Sweepin' Dreams 의 데미지 계산 규약. 본 문서는 **추천 공식 제안** 단계이며, 실제 값 튜닝과 일부 구조는 밸런싱에서 확정.

## 1. 메타

| 항목 | 값 |
|---|---|
| 시스템 ID | `damage-formula` |
| 분류 | 전투 |
| 의존 레이어 | Domain / Adapter (Skill, Enemy, Boss, Player) |
| 최종 업데이트 | 2026-04-18 (초안) |
| 상태 | 🟡 제안 — 밸런싱 검토 필요 |

## 2. 목적

모든 데미지 발생 경로(스킬 적중, 트리거 효과, DoT, 보스 접촉, 반사 등)에서 **일관된 계산 규칙**을 적용하기 위한 SSOT. 하드코딩·스킬별 중복 로직 방지.

## 3. 적용 범위

| 경로 | 본 공식 적용 여부 |
|---|---|
| 스킬 적중 (OnHit 메인 피해) | ✅ |
| TriggerEffect `DealDamage` / `Explode` / `Chain` (즉시) / `ApplyDoT` (틱) | ✅ |
| AreaZone 틱 피해 | ✅ |
| 적 → 플레이어 접촉 / 원거리 공격 | ✅ (역방향, 공격자=적) |
| 반사 데미지 (거울 반사, 최대 체력 반사 패시브 등) | ✅ (본 공식의 최종값을 기반으로 반사 비율 적용) |
| 처형 (`Execute`) | ❌ 공식 계산 후 별도 HP 비율 판정 |

## 4. 공식 (제안)

### 4.1 최종 데미지

```
final = base
      × (1 + attackPower * ATTACK_POWER_COEF)      // 공격력 패시브
      × critMult                                    // 치명타 여부
      × typeVsType                                  // (옵션) 속성·타입 상성
      × vulnerabilityMult                           // 취약 디버프 (ApplyVulnerability)
      × teamSynergyMult                             // (옵션) 혼돈 '단결' 등 멀티 시너지
      × (1 - defender.damageReduction)              // 방어력·저항 (0~1, 1은 무피해)
      + flatBonus                                   // 고정 추가 피해 (정수 등)

final = max(1, floor(final))                        // 최저 1, 정수 반올림(내림)
```

### 4.2 세부 항목

| 기호 | 의미 | 출처 |
|---|---|---|
| `base` | 스킬 SO 의 `Damage` × 레벨 스케일링 | `SkillData.progression` |
| `attackPower` | 플레이어 공격력 스탯 (패시브 #5 등) | `PlayerStats.attackPower` |
| `ATTACK_POWER_COEF` | 공격력 1당 증가율 (제안: **0.1** = +10%) | `GameplayConfig` |
| `critMult` | 치명타 발동 시 배율 (제안 기본: **1.5**, 패시브 #8 로 증가) | `PlayerStats.critDamage` |
| `typeVsType` | (옵션) 속성·타입 상성. 프로토타입 단계에서는 **1.0 고정** | 추후 확장 |
| `vulnerabilityMult` | `DebuffMark` 부착 시 배율 (기본 1.0, 마크 있을 때 1.3~1.5) | `DebuffMark` |
| `teamSynergyMult` | 혼돈 '단결' 등 | 런타임 TriggerEffect |
| `defender.damageReduction` | 방어력을 0~1 로 환산 (아래 4.4) | `Enemy.defense`, `Player.defense` |
| `flatBonus` | 정수/무기의 고정 추가 (예: 불 정수 DoT 별도) | `SkillTriggerSystem` runtime |

### 4.3 치명타 판정

```
isCrit = Random01() < clamp01(critChance)
critMult = isCrit ? playerStats.critDamage : 1.0
```

- `critChance` 기본 0, 패시브 #15 (치명타 확률 증가) 로 누적.
- 자동포탑은 **항상 치명타** (`isCrit = true` 강제, `PlacedTurret` 내에서 플래그 설정).

### 4.4 방어력 → 피해 감소 환산 (제안)

간단한 소프트캡 공식:

```
damageReduction = defense / (defense + K)
```
- `K` = 상수 (제안: **100**). `defense=100` → 50% 감소, `defense=300` → 75% 감소 등.
- 일반 적은 `defense=0` 으로 영향 없음. 둔한형 같은 저항 적에 조금만 부여 가능.
- 플레이어 방어력 패시브(#12)는 이 공식을 통해 적→플레이어 피해에 반영.

**대안:** 직접 비율(예: `0.1 = 10%`). 단순하지만 스택 시 100% 초과 버그 가능 → **소프트캡 권장.**

## 5. 역방향: 적이 플레이어 공격

동일 공식을 사용하되 `attacker = enemy`, `defender = player`:

```
damageTakenByPlayer = final(attacker=enemy, defender=player)
damageTakenByPlayer = max(1, damageTakenByPlayer - player.barrier)  // 배리어/일시 효과
```

- 플레이어의 `damageReduction` = `defense / (defense + K)`
- 피격 시 무적 시간 (패시브 #16) 은 공식 밖에서 처리 (피격 자체를 무효화).

## 6. 반사 데미지

혼돈 '거울 반사', 최대 체력 비례 반사 등:

```
reflected = damageTakenByPlayer * reflectRatio
// 또는 최대 체력 기반
reflected = maxHp * baseReflectRate + damageTakenByPlayer * reflectRatio
```

- 반사 데미지는 다시 **적에게 본 공식 적용 (attacker=player)** 으로 판정. 무한 루프 방지: **반사는 체인 반사 금지**.

## 7. DoT / 틱 피해

```
tickDamage = final(base=tickBase, ...)
```
- 본 공식을 틱마다 호출. `vulnerabilityMult` 등 부착된 디버프는 틱마다 재평가.
- DoT 는 적 개체에 부착 — 부착 시점의 공격자 스탯을 스냅샷 할지, 매 틱 재계산할지는 **매 틱 재계산 (단, 공격자 소멸 시 현재 값 유지)** 권장.

## 8. 연쇄 감쇄 (Chain)

ChainHandler 전이 시 **80% 감쇄** (현재 코드 상수):

```
chainDamage(n) = final * 0.8^n     // n: 이번 전이가 몇 번째 (0-based)
```

- 현재 `0.8` 은 하드코딩. **SO 필드로 노출 권장** (`chainDecayRatio`). [trigger-effects.md § 3.3](trigger-effects.md) 의 알려진 제약 참조.

## 9. 예외·특수 규칙

| 규칙 | 설명 |
|---|---|
| 최저값 보장 | `final < 1` 이면 **1 데미지**. 단 무적/저항 100% 상태면 0 가능. |
| 반올림 | `Mathf.FloorToInt(final)` 사용. 소수점 누적으로 인한 인플레 방지. |
| 처형 | `Execute` 핸들러는 본 공식과 별도로 `target.currentHp ≤ maxHp * threshold` 이면 즉사. 보스 제외. |
| 치명타 중복 (단일 적중 내) | 동시에 여러 치명타 소스가 있어도 `critMult` 는 한 번만 적용. |
| 치명타 재판정 (체인 / 연쇄폭발) | **노드별 재판정.** ChainHandler 가 다음 타겟으로 전이하거나 연쇄폭발이 새 폭발 노드를 만들 때마다 `Random.value < critChance` 를 새로 굴림. `critMult` 도 노드별 독립 적용. (예: 체인 5회에서 1회만 치명타가 나올 수 있음.) **WHY:** 노드별 재판정이 "치명타 확률 30%" 의 직관(매 타격마다 30%)에 부합하고, 체인 길이가 긴 스킬의 페이오프 가치도 보존. |
| DoT 치명타 | DoT 부착 **시점에 1회 판정**. 판정 결과를 DoT 인스턴스에 스냅샷하여 모든 틱에 동일 적용 (틱마다 재판정 안 함). **WHY:** 매 틱 재판정 시 RNG 의존 분산이 너무 커지고, 적색/금색 팝업이 같은 마크에서 섞여 시각적 혼란. |
| DoT 중복 | 같은 source 의 DoT 는 갱신, 다른 source 는 스택 (상한 검토 필요). |

## 10. 데이터 출처

- **ScriptableObject:** `Assets/Data/Skills/*.asset` (base, progression), `Assets/Data/Enemies/*.asset` (defense), `Assets/Data/GameplayConfig.asset` (coefficient 상수)
- **런타임 상태:** `PlayerStats`, `DebuffMark`, `SkillTriggerSystem.runtimeEffects`
- **튜닝 상수 (GameplayConfig):**
  - `ATTACK_POWER_COEF` (기본 0.1)
  - `DEFENSE_K` (기본 100)
  - `CRIT_MULT_BASE` (기본 1.5)
  - `CHAIN_DECAY` (기본 0.8)

## 11. 네트워크

- **데미지 판정 주체: 호스트.** 클라이언트는 스킬 발동 알림만 전송, 데미지 최종값은 호스트가 공식 적용 후 RPC 로 결과 전파.
- 각 클라는 수신한 결과로 피격 이펙트·HP 바 갱신.
- 규약 상세 [network-sync.md](network-sync.md).
- **치명타 판정 주체 (R9 Phase A 정책, 2026-04-26 도입):**
  - 데미지 사이트(`Projectile`/`AreaZone`/`OrbitalObject`/`PlacedTurret`)에서 **자기 측이 굴린 isCrit 를 채택** (호스트/클라 self-judging).
  - 클라가 자기 투사체로 적중 시: 자기 측 굴린 finalDamage + isCrit 를 `RequestDamage(...,isCrit)` 또는 `Boss.RequestDamageFromClient(damage, isCrit)` RPC 로 호스트에 전달 → 호스트는 그대로 적용 (재굴림 없음). 호스트 화면 색상도 클라 결과와 일치.
  - **사이드이펙트:** 다른 클라 화면(자기 투사체 아님)은 owner 굴림 결과 모름 → 일반 색상 표시. **Phase B 작업으로 전체 broadcast 동기화 예정.**
  - **TriggerEffect 핸들러** (`DealDamage`/`Explode`/`Chain`/`ApplyDoT`/`DamageNearby` 등) 는 호스트만 `CritJudgment.Roll` 굴림. 클라 fire 시엔 일반 데미지 fallback (양측 분기 회피).
  - **WHY:** Survivors-like 협동 게임 cheat 위협 적음 + 응답성 우선. 정석 호스트 권위 대비 일관성은 약하나 게임 느낌 손해 없음.

## 12. 테스트

- **단위 테스트(예정):** 각 계수 조합에 대해 기댓값 검증 (예: `base=10, attackPower=50 → final=15`).
- **플레이 모드:**
  - 치명타 발동 시 1.5배 적용
  - ApplyVulnerability 중첩 여부
  - 소프트캡 경계값 (`defense=100 → 50%`)
  - 반사가 체인으로 재반사되지 않는지

## 13. 기존 코드 참조

- `Assets/Scripts/Adapter/Skill/TriggerEffects/Handlers/DealDamageHandler.cs`
- `Assets/Scripts/Adapter/Skill/TriggerEffects/Handlers/ExplodeHandler.cs`
- `Assets/Scripts/Adapter/Skill/TriggerEffects/Handlers/ChainHandler.cs`
- `Assets/Scripts/Adapter/Entity/Enemy/*.cs` (`TakeDamage` 진입점)
- `Assets/Scripts/Adapter/Entity/Player/PlayerHealth.cs`
- `Assets/Scripts/Data/GameplayConfig.cs`
- `Assets/Scripts/Domain/` (DamageCalculator 인터페이스 — 신규 설계 필요 시)

## 14. 알려진 제약 / 리스크

- [ ] `ChainHandler` 감쇄율 하드코딩 (0.8) — SO 노출 필요
- [ ] DoT 스냅샷 vs 매 틱 재계산 정책 확정 필요
- [ ] DoT 스택 상한 미정
- [ ] 플레이어 간 데미지 공유(만약 팀 데미지 표시 기능이 생긴다면) 정책
- [ ] 반사 체인 방지 로직이 코드 상 확실히 있는지 검증
- [x] 치명타 중복 발동 정책 (동시 여러 소스일 때) 명시 — § 9 결정 (단일 적중 내 1회)
- [x] 치명타 재판정 정책 (체인 / 연쇄폭발) 명시 — § 9 결정 (노드별 재판정)
- [x] DoT 치명타 정책 명시 — § 9 결정 (부착 시점 1회 판정, 틱 동안 스냅샷 유지)

## 15. 변경 이력

- 2026-04-18: 초안 작성 — 기본 공식·소프트캡 방어·치명타·DoT·체인·반사 항목 정의
- 2026-04-25: 치명타 정책 3종 확정 — (1) 단일 적중 내 1회, (2) 체인/연쇄폭발 노드별 재판정, (3) DoT 부착 시점 스냅샷. R9 작업의 선행 결정으로 기록 (사용자 확정).
