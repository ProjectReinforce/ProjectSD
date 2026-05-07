# 적 설계서: 조롱꾼 (Mocker)

> ⚠️ **이름은 임시.** 사용자 결정에 따라 변경 가능. 후보: `Mocker / 조롱꾼`, `Harlequin / 할리퀸`, `Jeerer / 야유자`. 변경 시 ID(`enemy_quest_mocker`) / 파일명(`mocker.md`) / SO 경로 / 영어 표기 일괄 갱신 필요.

## 1. 메타

| 항목 | 값 |
|---|---|
| 엔티티 ID | `enemy_quest_mocker` |
| 한국어 이름 | 조롱꾼 |
| 영어 이름 | Mocker |
| 분류 | **퀘스트 전용 (신규 카테고리)** — 일반 / 빠른형 / 둔한형 / 무리형 / 원거리 / 엘리트 / 보스와 별도 |
| 등장 시점 | **퀘스트 트리거 시** (시간 무관) — 일반 스폰 풀에 섞이지 않음 |
| 구현 상태 | ⬜ 설계만 |
| 최종 업데이트 | 2026-05-07 |

## 2. 컨셉

플레이어를 피해 도망치다가 거리가 충분히 벌어지면 멈춰 서서 춤을 추며 조롱하는 비전투 NPC급 몹. **공략 자체가 협동을 통한 몰이 사냥**이 되도록 설계 — 1인이 단순 추격으로는 잡기 어렵고, 파티 멤버가 양쪽/포위 형태로 압박해야 효율적으로 잡힘. 처치 시 능력치 부스트 카드를 보상으로 준다.

데미지를 주지 않는 비전투형이라 기존 적 카테고리 4종(Chaser/Runner/Tank/Swarm) + Ranged + Elite 와 행동 설계가 근본적으로 다르다.

## 3. 스탯

> 모든 수치 TBD (밸런싱 단계). 권장 시작값만 표기.

| 레벨 | HP | 데미지 | 이속 | 공격 범위 | 점수(EXP) | 기타 |
|---|---|---|---|---|---|---|
| 1 (시작 안) | TBD (~150) | **0** | TBD (~0.7) | — | TBD (0~5) | 시간 제한 60s 안에서 협동으로 잡을 수 있는 두께 |

수식/스케일링:
- **인원 스케일링은 적용** ([INDEX.md § 인원 스케일링](INDEX.md)) — 4인이면 HP ×1.8 자연 증가
- **시간 스케일링은 적용 안 함** — 퀘스트 트리거 시점이 곧 스폰 시점이므로 게임 시간 의존 무의미
- **이속 권장**: 플레이어 평균 이속(0.6 안) 대비 약간 빠름(0.65~0.7). 1인 추격으론 잡기 빡빡, 협동 시 잡힘

## 4. 이동 패턴

- **이동 타입:** 신규 **`EvadeStrategy`** (또는 `MockerEvadeMovement`)
- **참조:** `Features/Enemy/Adapter/Movement/` (신규 클래스)
- **추적 대상:** 모든 살아있는 플레이어 회피 (단일 타겟 X)
- **특수 동작:** ✓ 가중치 도주 ✓ Taunt(춤) 상태 머신 ✓ 거점 반경 가드

### 4.1 도주 알고리즘 (가중치 합산)

매 프레임(또는 N프레임마다) 도주 방향을 가중치 합산으로 결정:

```csharp
Vector2 ComputeFleeDirection() {
    Vector2 sum = Vector2.zero;
    foreach (Player p in alivePlayers) {
        Vector2 away = (transform.position - p.position);
        float distSqr = Mathf.Max(away.sqrMagnitude, minDistSqr);
        sum += away.normalized * (1f / distSqr);   // 역제곱 가중 — 가까운 플레이어 영향 ↑
    }

    // 거점 반경 가드 (맵 경계 미구현 대체)
    Vector2 toCenter = (questZoneCenter - transform.position);
    float distFromCenter = toCenter.magnitude;
    if (distFromCenter > zoneRadius * 0.8f)
        sum += toCenter.normalized * boundaryPullStrength;

    // 관성 (지그재그 방지)
    sum += lastDirection * inertiaWeight;

    // 멈춤 방지 — 합벡터가 거의 0이면 직전 방향 유지
    if (sum.sqrMagnitude < epsilon)
        return lastDirection;

    lastDirection = sum.normalized;
    return lastDirection;
}
```

**의도:**
- 역제곱 가중 → 두 플레이어가 양쪽에서 압박 시 **둘 사이 가장 먼 갭**으로 자연 도주 (사용자 요청 #2)
- 거점 반경 가드 → 맵 경계 미구현 단계에서 무한 도주 방지 (사용자 요청 #3)
- 관성 + 멈춤 방지 → 양쪽 플레이어가 정확히 대칭 위치일 때 합벡터 0 으로 굳어지는 케이스 방지

### 4.2 상태 머신

| State | 동작 | Animator |
|---|---|---|
| **Fleeing** (기본) | 4.1 알고리즘으로 매 프레임 이동 | `IsMoving=true` |
| **Taunting** (도발) | 정지 + 춤 | `Taunt` trigger 발화, `IsMoving=false` |
| Death | 사망 | 표준 `Die` trigger |

### 4.3 상태 전이

```
Fleeing → Taunting:
    minDistanceToAnyPlayer ≥ tauntDistance

Taunting → Fleeing (둘 중 빠른 쪽 — 사용자 결정 #4 옵션 C):
    (a) minDistanceToAnyPlayer ≤ resumeDistance      // 한 명만 다가가도 도주 재개
    (b) tauntDuration 경과                            // 시간만으로도 도주 재개
```

`tauntDistance > resumeDistance` 히스테리시스로 경계에서 토글 방지 (예: 8 / 4).

**도발 중 받는 데미지 배율: ×1** (사용자 결정 #5) — 멈춰 있을 뿐, 카운터 보너스 없음.

## 5. 공격 패턴

**없음.** 데미지를 주지 않는 비전투형. `EnemyAttack` 컴포넌트 부착 안 함 / `EnemyContact` 의 데미지 적용 분기 가드 필요.

## 6. 보상

- **경험치:** TBD (0 또는 소량 — 퀘스트 보상이 본체이므로)
- **드랍:** **능력치(스탯 부스트) 선택지** ([quest.md § 3.5](../quest.md)) — 4등급 가중치 동일
- **처치 이벤트 트리거:** 퀘스트 시스템이 `OnMockerKilled(questId)` → 완료 판정 → `RPC_QuestCompleted`

## 7. 데이터 계약 (ScriptableObject)

> 옵션 권장: **신규 `MockerData : EnemyData`** 서브타입. 기존 적 SO 영향 없이 Mocker 전용 필드만 분리.

```
Assets/Data/Enemy/Mocker/enemy_quest_mocker.asset
MockerData : EnemyData
  // EnemyData 상속 필드 (HP, Speed, Damage=0, Score, animatorController, pivotOffsetX 등)
  + tauntDistance       : float   // Fleeing → Taunting 임계 (예: 8)
  + resumeDistance      : float   // Taunting → Fleeing 거리 임계 (예: 4)
  + tauntDuration       : float   // Taunting → Fleeing 시간 임계 (예: 4)
  + zoneRadius          : float   // 거점 반경 가드 (예: 거점 진입 반경 × 3)
  + boundaryPullStrength: float   // 거점 중심 인력 강도 (예: 0.7)
  + inertiaWeight       : float   // 관성 가중치 (예: 0.4)
  + minDistSqr          : float   // 역제곱 발산 방지 하한 (예: 1.0)
```

대안: EnemyData 에 옵션 필드로 직접 추가 — 모든 적 SO 인스펙터에 빈 필드가 노출되어 혼란. 비추.

## 8. 네트워크 동기화

네트워크 기본 규약은 [systems/network-sync.md](../../systems/network-sync.md).

- **스폰 주체:** 호스트 (퀘스트 시작 RPC 시점에 `SpawnManager` 경유)
- **AI 실행 주체:** 호스트 + 클라 각자 (Dead Reckoning — [network-sync.md § 8.1](../../systems/network-sync.md))
- **상태 전이 RPC:**
  - `RPC_MockerTaunt(enemyId, isTaunting)` — 호스트 결정 후 모든 클라 전파. Animator `Taunt` trigger / `IsMoving` 토글
  - 사망 = 기존 enemy death RPC 재사용
- **가중치 도주의 결정성:** 모든 살아있는 플레이어 위치 기반이라 호스트/클라 각자 시뮬레이션 결과가 어느 정도 수렴. 미세 오차는 기존 호스트 위치 RPC 보정 주기로 흡수 가능

## 9. UI / 비주얼

- **인디케이터:** [WorldIndicator](../../systems/world-indicator.md) 의 **`WhileActive`** 카테고리 사용 (확정, 사용자 결정 2026-05-07). 퀘스트 진행 중 In-Screen 머리 위 이름표 + Off-Screen 화살표 표시
- **춤 애니메이션:** [character-animation.md § 4](../../systems/character-animation.md) 표준 파라미터에 **`Taunt` (Trigger) 추가**. Mocker 전용 클립으로 별도 base controller 또는 Override
- **춤 사운드 / 이펙트:** TBD

## 10. 구현 체크리스트

- [ ] `MockerData : EnemyData` SO 클래스 정의 (코드 작성)
- [ ] `Assets/Data/Enemy/Mocker/enemy_quest_mocker.asset` SO 인스펙터 작성 (사용자 작업)
- [ ] `EvadeStrategy` / `MockerMovement` Movement 구현 — 가중치 도주 + 거점 반경 가드 + 멈춤 방지
- [ ] Mocker 상태 머신 (Fleeing / Taunting) + `RPC_MockerTaunt`
- [ ] `EnemyContact` 의 데미지 적용 분기 가드 (Mocker 는 데미지 X)
- [ ] Animator: 표준 파라미터에 `Taunt` 추가 + Mocker base controller / Override
- [ ] Quest 시스템: `HuntMocker` QuestType + 핸들러 ([quest.md](../quest.md))
- [ ] WorldIndicator 등록 — `WhileActive` 정책 (확정)
- [ ] `photon-sync-auditor` (Taunt RPC + 도주 시뮬레이션 결정성)
- [ ] `unity-reviewer` (생명주기 + null 안전 + EnemyContact 가드)
- [ ] 플레이테스트 — 1/2/3/4인 협동 몰이 사냥 난이도 확인

## 11. 오픈 이슈

- [ ] **이름 확정** — Mocker / Harlequin / Jeerer / 그 외
- [ ] **HP / 이속 / EXP 수치** — 60s 시간 제한 + 인원 스케일링 고려한 두께·속도 (밸런싱)
- [ ] **`tauntDistance` / `resumeDistance` / `tauntDuration` 정확값** — 8 / 4 / 4 (안)
- [x] **MockerData 분리 vs EnemyData 옵션 필드** — `MockerData : EnemyData` 서브타입 분리 (확정, 2026-05-07). SkillData → ProjectileSkillData 와 동일 패턴
- [x] **인디케이터 표시** — `WhileActive` 카테고리 사용 (확정, 2026-05-07)
- [x] **거점당 동시 등장 = 1마리** (확정, 2026-05-07)
- [ ] **거점 재사용** — 한 번 완료한 거점에서 다시 등장하나? ([quest.md § 9](../quest.md) 와 동일 이슈)
- [ ] **맵 경계 의존성** — `map-bounds` 미구현 단계에서는 zoneRadius 가드 사용. 맵 확정 후 `BossSpawner` 처럼 `mapBoundsCollider` 가드 추가 검토 ([map-bounds.md](../../systems/map-bounds.md))
- [ ] **춤 클립** — 새 도트 에셋 필요 / 기존 sanctum 의 emote 재활용 가능 여부
- [ ] **사운드 / VFX** — 도발 시 호객 음성, 환호 같은 차별화

## 12. 관련 문서

- [quest.md § 3.4](../quest.md) — `HuntMocker` 퀘스트 타입
- [INDEX.md](INDEX.md) — 적 분류표 등록
- [stat-boost.md § 5.2](../stat-boost.md) — 처치 보상 RPC 경로
- [character-animation.md § 4](../../systems/character-animation.md) — `Taunt` 트리거 추가
- [world-indicator.md](../../systems/world-indicator.md) — 인디케이터 정책 (검토 대상)
- [map-bounds.md](../../systems/map-bounds.md) — 맵 경계 가드 (미구현 의존)
- [network-sync.md § 8.1](../../systems/network-sync.md) — Dead Reckoning + 호스트 위치 RPC 보정
