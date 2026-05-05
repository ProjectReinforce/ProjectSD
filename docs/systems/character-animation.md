# 시스템 명세서: 캐릭터/적 애니메이션

## 1. 메타

| 항목 | 값 |
|---|---|
| 시스템 ID | `character-animation` |
| 이름 | 캐릭터/적 2D 스프라이트 애니메이션 |
| 분류 | 비주얼 / Adapter |
| 의존 레이어 | Adapter (Animator 핸들러), Data (SO 의 RuntimeAnimatorController 필드) |
| 구현 상태 | 🟡 Phase 1 인프라 완료 (코드), 점진 도입 (Unity 측 controller/Override 작업 진행 중) |
| 최종 업데이트 | 2026-05-02 |

## 2. 목적

대기실 + 인게임 캐릭터(LobbyPlayer / PlayerStub) 와 적(Enemy) 의 2D 스프라이트 애니메이션을 통합 시스템으로 관리. Sweepin' Dreams 의 첫 애니메이션 시스템.

해결하는 문제:
- 정적 sprite 만으론 시각적 빈약함 (이동·공격·사망 시 동일 sprite)
- 캐릭터 4종 + 적 N종 + 보스 각자 다른 클립을 가짐 → 공통 base + 캐릭터별 Override 가 유지보수 효율적
- Survivors-like 다수 동시 존재 (적 90마리+) 풀링 환경에서 Animator state 잔류 방지 필요

## 3. 핵심 정책

| 항목 | 정책 |
|---|---|
| **상태 머신 패턴** | 공통 base AnimatorController + 캐릭터/적별 `AnimatorOverrideController` 로 클립만 override |
| **State 구성 (1차)** | `Idle`, `Walk`, `Death` 3-state. 공격은 적은 EnemyAttack 사이드, 캐릭터는 미정 (스킬 발사가 시각화) |
| **방향 분기** | **Phase 1: 2방향** (정면/측면 + 측면을 좌/우 flipX) → **Phase 2: 4방향** (Blend Tree, MoveX/MoveY 사용) |
| **피격 비주얼** | Animator hit state 도입 안 함. 기존 `PlayerVisual` / `Enemy` 의 빨간 깜빡임 + DamagePopup 으로 충분 |
| **부활 시퀀스** | Stand 복귀 + 부활 이펙트 + `PlayerMovement.SetInputLocked` + i-frame (R7) — 신규 Revive 클립 또는 단순 Stand 복귀 (선택) |
| **GameState.Paused 시 정지** | `animator.speed = 0` (timeScale 정책 X 라 Animator 자동 정지 안 됨) — 레벨업·ESC 솔로 메뉴 시 idle 클립 계속 재생 어색함 회피. `GameOver/GameClear` 는 Die 진행을 위해 정지 X |
| **풀링 환경 (적 한정)** | `EnemyAnimator.OnReturnToPool` 에서 `Animator.Rebind()` — Death state 가 다음 스폰에 잔류 방지 |
| **결과창 진입 지연** | 사망 애니메이션 클립 길이만큼 결과창 표시 지연 (`GameplayConfig.resultPanelDelay`, default 1.5s) |

## 4. 표준 AnimatorController 파라미터

```
IsMoving (Bool)    Idle ↔ Walk
Die      (Trigger) Any State → Death
Revive   (Trigger) Death → Idle (옵션, 클립 없으면 무시됨, 캐릭터 전용)
Attack   (Trigger) Any State → Attack (옵션, 적/보스 전용 — 공격 모션)
MoveX    (Float)   4방향 Blend Tree 용 (정규화 -1~1)
MoveY    (Float)   동상
```

`Attack` 트리거는 적의 경우 **Ranged 타입만** 발화한다 (근접형은 접촉 데미지 자체가 시각 공격).
`runtimeAnimatorController` 또는 해당 파라미터가 미설정인 컨트롤러는 `SetTrigger` 가 무시되어 안전.

**Transition 권장값**:
- Has Exit Time = OFF (즉시 반응)
- Transition Duration = 0 (체감 지연 제거)
- Conditions = parameter 매칭

## 5. 인터페이스 (코드)

### `Features/Character/Adapter/PlayerAnimator.cs`

```csharp
public class PlayerAnimator : MonoBehaviour
{
    public void Bind(PlayerHealth health);          // Health 이벤트 구독 (Die/Revive)
    public void ApplyCharacter(CharacterData data); // controller 주입 + Animator.Rebind()
    public void TriggerRevive();                     // RespawnManager 부활 시퀀스용
}
```

### `Features/Enemy/Adapter/EnemyAnimator.cs`

```csharp
public class EnemyAnimator : MonoBehaviour
{
    public void Bind(Enemy enemy, EnemyData data); // OnDied 구독 + controller + pivotOffsetX 주입
    public void OnReturnToPool();                   // Animator.Rebind() — Enemy.OnReturnToPool 에서 호출
    public void TriggerAttack();                    // SpawnManager.RPC_TriggerEnemyAttack 경로로 외부 발화
}
```

### `Features/Boss/Adapter/BossAnimator.cs`

```csharp
public class BossAnimator : MonoBehaviour
{
    public void Bind(Boss boss, BossData data); // OnDied 구독 + controller + pivotOffsetX 주입
    public void TriggerAttack();                 // Boss.RPC_TriggerAttack 경로로 외부 발화
}
```

보스는 풀링되지 않아 `OnReturnToPool` 미보유. `Boss.Initialize` 와 `InitializeFromNetwork` 양쪽에서 `Bind` 호출 (호스트/클라 모두 controller 와 OnDied 구독 필요).

### `Features/UI/Adapter/Menu/LobbyPlayerController.cs` (대기실 전용)

`LobbyPlayerController` 안에 Animator 캐싱 + `ApplyOwnerCharacter` 시 controller swap + Update 에서 IsMoving + flipX 토글. PlayerAnimator/EnemyAnimator 와 별도 (대기실은 PlayerHealth 등 의존성 없음).

## 6. 데이터 흐름

```
[CharacterDatabase / EnemyData SO] (animatorController 필드)
        ↓
[GameManager.CharacterDB.GetById]
        ↓
[PlayerStub.Initialize / Enemy.Initialize]
        ↓
[PlayerAnimator.ApplyCharacter / EnemyAnimator.Bind]
        ├─ animator.runtimeAnimatorController = data.animatorController
        └─ animator.Rebind()  ← swap 직후 stale parameter/trigger 정리
```

대기실은 `LobbyPlayerController.ApplyOwnerCharacter` 가 동일 흐름 (`characterDB` 인스펙터 직접 참조).

### 6.1 단일 프리팹 + SO swap 패턴 (적/캐릭터 공통)

일반 적은 **`Assets/Resources/Prefabs/Enemy/EnemyBase.prefab` 하나**로 모든 변형(Chaser/Runner/Tank/Swarm/Ranged/Elite) 운용. 다양성은 `EnemyData` SO 가 담당하고, 단일 프리팹이 풀에서 재사용되며 매 스폰마다 SO 가 바뀐다.

```
SpawnManager.enemyPrefab  (단일 SerializeField)
        ↓ PoolManager.Get
[EnemyBase 인스턴스]  ← 풀 재사용
        ↓ Enemy.Initialize(id, data, ...)
[EnemyAnimator.Bind(enemy, data)]
        ├─ data.animatorController 주입
        ├─ pivotOffsetX 갱신 + ApplyPivotOffset 재적용
        └─ Animator.Rebind()  ← 이전 SO 의 stale state/trigger 정리 (필수)
```

**함의:**
- 프리팹 셋업 작업 (Visual 자식 분리 등) 은 **EnemyBase 한 번만** 하면 모든 적 변형에 자동 적용.
- AnimatorController 점진 도입도 **SO 별로** — 같은 GameObject 가 매 스폰 controller 갈아탐.
- 풀 반환 시 `Animator.Rebind()` 가 필수 (다음 스폰의 다른 SO 와 충돌 방지). 자세한 함정은 § 9.

캐릭터(`PlayerStub.prefab`) 도 동일 — 단일 프리팹에 `CharacterData` 주입으로 4종 캐릭터 분기. 보스만 별도 프리팹(`Boss.prefab`).

## 7. 좌우 flipX 정책

`SpriteRenderer.flipX` 토글로 좌우 분기:

```csharp
if (Mathf.Abs(velocity.x) > 0.01f && spriteRenderer != null)
    spriteRenderer.flipX = defaultFacingRight ? velocity.x < 0f : velocity.x > 0f;
```

- `defaultFacingRight` 인스펙터 토글 (sprite 기본 향) — sanctum 측면 sprite 가 보통 우향이라 default true
- 위/아래만 입력 시 마지막 facing 유지
- 4방향 Blend Tree 마이그레이션 시 flipX 제거 + MoveX 음수도 정상 분기

### 7.1 피벗 보정 (`CharacterData.pivotOffsetX`)

비대칭 패딩 PNG 가 flip 시 좌우로 튕겨 보이는 현상 보정. PlayerAnimator /
LobbyPlayerController 양쪽이 flip 토글마다 SpriteRenderer 자식
`transform.localPosition.x` 로 ±보정 적용.

- **부호 컨벤션:** 기본 facing 상태에서 캐릭터를 시각 중심으로 옮기려면 어느
  방향으로 얼마나 밀어야 하는가. `defaultFacingRight=true` 이고 캐릭터가
  피벗보다 왼쪽으로 치우쳐 있으면 양수. flip 시 부호 자동 반전.
- **전제:** SpriteRenderer 가 root 가 아닌 자식 GO 에 있어야 함 — 보정으로
  본체(Rigidbody2D/Collider/Photon 동기화 transform) 가 통째로 움직이는 사고
  방지 가드. PlayerStub.prefab / LobbyPlayer.prefab 모두 `Visual` 자식 GO 에
  SR + Animator 배치.
- 0 입력 시 보정 비활성화 (기존 동작).
- 근본 해결 대안: 원본 PNG 재export 또는 Sprite Editor Custom Pivot. 런타임
  보정은 외부 패키지/도트 작가 자산을 그대로 쓰면서 해결할 때 유용.
- 현재값: 3캐릭터(Magician/Swordman/Thor) 모두 `0.05`.

### 7.2 피벗 보정 — 적/보스 확장 (`EnemyData.pivotOffsetX` / `BossData.pivotOffsetX`)

캐릭터와 동일 컨벤션 + 동일 메커니즘. EnemyAnimator/BossAnimator 양쪽이 PlayerAnimator 와
같은 `ApplyPivotOffset(bool flipped)` 로직 보유.

- **전제 동일:** SpriteRenderer 가 root 가 아닌 자식 GO 에 있어야 함. Chaser/Boss 등 적 프리팹은
  `Visual` 자식 GO 분리 필수 (root 에 두면 본체 transform 이 통째로 움직여 물리/네트워크 영향).
- **권장 시작값:** 캐릭터와 같은 sanctum 계열 에셋이라면 `0.05` 부터 시작. 0 이면 보정 없음.
- BossAnimator 는 `BossData.defaultFacingRight` 로 기본 향 분기 (BossData SO 의 인스펙터 토글).

## 8. 공격 애니 동기화 (Ranged 적 + 보스)

근접형 적은 접촉 데미지 자체가 공격이라 별도 모션 트리거 없음. **Ranged 적과 보스만**
명시적 `Attack` 트리거 사용.

### 8.1 Ranged 적

```
[EnemyAttack.FireOnce]                          ← 호스트만, GameState 가드 통과 후
        ↓
[SpawnManager.RaiseEnemyAttackAnim(enemyId, facingLeft)]  ← 호스트 → All RPC
        ↓ facingLeft = (target.x - enemy.x) < 0  (호스트 결정)
[RPC_TriggerEnemyAttack(enemyId, facingLeft)] (모든 클라)
        ↓
[EnemyAnimator.FaceDirection(facingLeft)]       ← flipX + 피벗 보정 갱신
[EnemyAnimator.TriggerAttack()]                  ← Attack 트리거
        ↓
[Animator.SetTrigger(Attack)]
        ↓ (직후)
[RaiseEnemyProjectile / RaiseTelegraph] — 같은 SpawnManager PV 라 수신측 순서 보장
```

`Enemy` 는 PhotonView 가 없어 RPC 직송 불가 → `SpawnManager.photonView` 경유. enemyId 로
`activeEnemies` Dictionary 매칭. 미매칭(클라가 아직 spawn RPC 못 받음) 이면 silently drop —
같은 PV 로 송신되므로 Spawn → Attack 순서가 Photon 에 의해 보장돼 실무상 발생 X.

### 8.2 보스

```
[BossPhaseManager.UpdateAttacks] (호스트만)
        ↓ entry.pattern.CanExecute → 공격 실행 결정
[currentBoss.RaiseAttackAnim()]                 ← Boss.photonView 경유 RPC
        ↓
[RPC_TriggerAttack] (모든 클라)
        ↓
[bossAnimator.TriggerAttack()]
        ↓
[entry.pattern.Execute(...)]                    ← 내부에서 경고존/투사체 RPC 송신
```

**호출 순서 의도:** 모션 트리거가 패턴 Execute 보다 **먼저** 발화. CircleZone/Shockwave 의
경고존/투사체 RPC 가 모션 직후 도착하도록 — 의미상 "보스가 휘두름 → 경고 표시" 순서.

### 8.3 트리거 손실 시나리오

`Attack` 트리거는 `RpcTarget.All` 송신 (buffered 아님). 늦참 클라가 송신 직후 join 하면
첫 1회 미스 가능성 있음 — **수용**: 다음 cooldown 사이클에서 자동 복구. 게임플레이 영향 미미.

## 9. 풀링 함정 (적 전용)

`Enemy.OnReturnToPool` 호출 순서:
```
1. enemyAnimator.OnReturnToPool()  ← Animator.Rebind()
2. ... 기타 reset ...
3. gameObject.SetActive(false)     ← 마지막
```

**중요**: `Animator.Rebind()` 는 `gameObject.SetActive(false)` 이후 호출하면 Unity 가 무시 + 경고. 순서 절대 바꾸지 말 것 (`EnemyAnimator.cs:88` 주석에 명시).

## 10. 깨진 sprite reference 검증 도구

`Assets/Scripts/Editor/AnimationClipValidator.cs` — sanctum 같은 외부 에셋 import 시 일부 .anim 의 sprite reference 가 깨진 케이스 일괄 스캔.

사용:
1. Project 창에서 검사할 폴더 선택 (예: `Assets/FromStore/sanctum_pixel`)
2. 메뉴 `Tools → Validate AnimationClip Sprites (Selected Folder)`
3. Console 에 깨진 .anim 목록 + frame index 출력

판정 기준: `m_Sprite` curve 의 어느 한 frame 이라도 reference == null 이면 카운트.

**False positive 가능**: sanctum effect 클립이 의도적으로 마지막 frame 에 null 두는 패턴. 진짜 broken 클립만 재생 시 sprite=null 로 set 되어 캐릭터 안 보임 (Magician 테스트 케이스).

## 11. 점진 도입 (Phase)

### Phase 1: 코드 인프라 ✅ (2026-05-01)
SO 필드 + PlayerAnimator + EnemyAnimator + LobbyPlayerController 수정 + PlayerMovement.SetInputLocked + PlayerStub/Enemy 통합. SO 의 controller 가 비어있으면 정적 sprite (기존 동작 유지) — 점진 도입 안전.

### Phase 2: 캐릭터 4종 controller + Override (사용자 작업)
- 옵션 C (혼합 종족): hero / archer / wizard / scyther
- base controller 1개 + 캐릭터별 Override 4개
- 각 `CharacterData_*.asset` 의 `animatorController` 슬롯 연결

### Phase 3: 적 N종 controller + Override (사용자 작업)
- **단일 `EnemyBase.prefab` + `EnemyData` SO swap** 구조 (§ 6.1) — 프리팹 추가 없이 SO 별로 controller 만 갈아끼움
- 적 패키지별 base controller + 변형별 Override
- `EnemyData_*.asset` 의 `animatorController` 슬롯 연결
- 풀링 환경 검증 (`Animator.Rebind()` 동작 — 다른 SO 로 재스폰 시 stale state 잔류 없는지)

### Phase 4: 보스 별도 controller (14_big_monster_bundle)
보스는 단일 정면 + hit/dead/ready 등 상태 풍부 → 별도 base controller (`BossBase.controller`).
`BossAnimator` 컴포넌트 + `BossData.animatorController` 슬롯 + `pivotOffsetX` 셋업.
`BossData.defaultFacingRight` 로 기본 향 토글. Boss.prefab 에 `Visual` 자식 GO 분리 후 SR/Animator 이전 필요.

### Phase 5: 4방향 Blend Tree 마이그레이션
Idle/Walk state 를 Blend Tree 로 교체. MoveX/MoveY parameter 활용. flipX 로직 제거 가능.

## 12. 알려진 제약

- [ ] **부활 클립 미정** — sanctum 에 revive 전용 anim 없음. Stand 복귀 + 별도 ParticleSystem 으로 처리 예정 (사용자 부활 이펙트 결정 대기)
- [x] **원격 IsMoving (인게임 + 로비)** — PlayerAnimator/LobbyPlayerController 모두 IsMine 분기 + transform.position 프레임 차분으로 velocity 추정. PhotonTransformView 가 보간한 위치를 사용하므로 자연스러움 (RPC/직렬화 추가 없음).
- [ ] **PlayerStub 의 OnDestroy 미작성** — Health 람다 dangling 이론상 가능. 같은 GO 라 실무 영향 작음
- [ ] **Animator.runtimeAnimatorController = null 일 때 Update 비용** — 매 프레임 null 체크만 하지만 수백 마리 적 일 때 누적. 향후 `enabled = false` 토글 검토 (성능 nit)

### 12.1 공격 모션 정교화 Follow-ups (2026-05-05 발견)

Animator 도입 인게임 검증 중 식별된 후속 개선 항목. 모두 게임플레이 영향 있는 변경이라 별도 작업 단위로 처리.

- [ ] **공격 모션 중 이동 lock 미구현** — 현재 보스/Kite Ranged 가 `Attack` state 재생 중에도 `transform.position` 갱신 → 공격하면서 이동하는 어색한 모션. 개선안: `EnemyAnimator.IsAttacking()` / `BossAnimator.IsAttacking()` (`GetCurrentAnimatorStateInfo(0).IsName("Attack")` 검사) 추가 → `EnemyMovement.Update` / `Boss.Update` 의 이동 분기 가드. `RPC_TriggerEnemyAttack` / `RPC_TriggerAttack` 가 모든 클라 동시 발화하므로 자연 동기화. 보스 패턴 정식 구현 단계에서 같이 처리 가능.
- [ ] **Anti-overlap 끌림이 IsMoving 토글 유발** — `EnemyMovement.LateUpdate.ResolveEnemyOverlap` 의 collider resolve correction (`maxResolvePerFrame=0.05` → 60fps 기준 ~3 m/s) 만으로도 `transform.position` 이 변해 EnemyAnimator 의 위치 차분이 `IsMoving=true` 로 판정. 다른 적에 끌려갈 때 Walk 애니가 잘못 재생됨. 개선안: `EnemyMovement.IntendedVelocity` 프로퍼티 노출 (strategy 호출 직전/직후의 transform 차분만 캡처) → `EnemyAnimator.Update` 가 위치 차분 대신 `IntendedVelocity` 사용. 넉백/네트워크 보정/anti-overlap 모두 IsMoving 무관해짐. 일관성 위해 Boss 도 같은 패턴 검토.
- [ ] **공격 모션 타이밍과 투사체 발사 시점 미매칭** — 현재 `EnemyAttack.FireOnce` 가 `RaiseEnemyAttackAnim` 직후 동일 프레임에 `RaiseEnemyProjectile`/`RaiseTelegraph` 송신. "활시위 당기는 모션이 끝난 시점에 화살 발사" 같은 자연스러움 X. 개선안: Animation Event 패턴. Attack 클립의 발사 frame 에 `OnReleaseProjectile` 이벤트 등록 → `EnemyAttack` 에 발사 정보 캐싱 + 콜백에서 실제 spawn RPC. 호스트 가드 (`if (!PhotonNetwork.IsMasterClient) return;`) 필수, `Enemy.OnReturnToPool` 에서 `pendingFire` 리셋 필요. 보스도 같은 패턴 가능하나 페이즈/패턴 다양성 때문에 별도 작업.
- [x] **Ranged 적이 발사 시점에 플레이어 방향으로 face 안 함** ✅ (2026-05-05) — `RaiseEnemyAttackAnim(int, bool)` 시그니처 확장 + `EnemyAnimator.FaceDirection(bool)` 추가. 호스트가 `target.x - enemy.x` 부호로 facingLeft 결정 → 모든 클라가 RPC 수신 시 face 갱신 + Attack 트리거. Update 위치 차분 flipX 와 컨벤션 동일 (defaultFacingRight 기반) 이라 이동 재개 시 자연 인계. 보스는 항상 추적 이동 중이라 변경 불필요.

## 13. 기존 코드 참조

- 신규 컴포넌트:
  - [PlayerAnimator.cs](../../Assets/Scripts/Features/Character/Adapter/PlayerAnimator.cs)
  - [EnemyAnimator.cs](../../Assets/Scripts/Features/Enemy/Adapter/EnemyAnimator.cs)
  - [BossAnimator.cs](../../Assets/Scripts/Features/Boss/Adapter/BossAnimator.cs)
  - [AnimationClipValidator.cs](../../Assets/Scripts/Editor/AnimationClipValidator.cs)
- 수정된 컴포넌트:
  - [LobbyPlayerController.cs](../../Assets/Scripts/Features/UI/Adapter/Menu/LobbyPlayerController.cs)
  - [PlayerMovement.SetInputLocked](../../Assets/Scripts/Features/Character/Adapter/PlayerMovement.cs)
  - [PlayerStub.Initialize](../../Assets/Scripts/Features/Character/Adapter/PlayerStub.cs)
  - [Enemy.Initialize / OnReturnToPool](../../Assets/Scripts/Features/Enemy/Adapter/Enemy.cs)
  - [Boss.Initialize / RPC_TriggerAttack](../../Assets/Scripts/Features/Boss/Adapter/Boss.cs)
  - [BossPhaseManager.UpdateAttacks](../../Assets/Scripts/Features/Boss/Adapter/BossPhaseManager.cs) — 공격 패턴 Execute 직전 RaiseAttackAnim
  - [EnemyAttack.FireOnce](../../Assets/Scripts/Features/Enemy/Adapter/Attack/EnemyAttack.cs) — Ranged 발사 직전 RaiseEnemyAttackAnim
  - [SpawnManager.RaiseEnemyAttackAnim](../../Assets/Scripts/Shared/Managers/SpawnManager.cs) — Enemy attack 트리거 RPC
  - [PlayerVisual.OnDeadStateChanged](../../Assets/Scripts/Features/Character/Adapter/PlayerVisual.cs) — alpha=0.3 제거
  - [ResultManager.RPC_ShowResult](../../Assets/Scripts/Shared/Managers/ResultManager.cs) — resultPanelDelay 적용
- SO 필드:
  - [CharacterData.animatorController / pivotOffsetX](../../Assets/Scripts/Features/Character/Adapter/Data/CharacterData.cs)
  - [EnemyData.animatorController / pivotOffsetX](../../Assets/Scripts/Features/Enemy/Adapter/Data/EnemyData.cs)
  - [BossData.animatorController / pivotOffsetX / defaultFacingRight](../../Assets/Scripts/Features/Boss/Adapter/Data/BossData.cs)
- 관련 시스템:
  - [in-game-menu.md](in-game-menu.md) — GameState.Paused 정책 SSOT
  - [waiting-room.md](waiting-room.md) — LobbyPlayer 외관 흐름
  - [network-sync.md](network-sync.md) — 적 풀링 + Dead Reckoning

## 14. 변경 이력

- 2026-05-02: 초안. Phase 1 코드 인프라 완료. base controller 패턴 + flipX + GameState.Paused 시 정지 + 풀링 Rebind + 결과창 지연 명세.
- 2026-05-03: § 7.1 피벗 보정 메커니즘 추가 (`CharacterData.pivotOffsetX` + PlayerStub/LobbyPlayer Visual 자식 분리). PlayerAnimator/LobbyPlayerController 양쪽 적용.
- 2026-05-05: 적/보스 확장. § 7.2 적/보스 피벗 보정(`EnemyData.pivotOffsetX` / `BossData.pivotOffsetX`) + § 8 공격 애니 동기화 (Ranged 적 + 보스, RPC 흐름) 신설. EnemyAnimator 에 `Attack` 트리거 + `TriggerAttack()` + `FaceDirection(bool)`, BossAnimator 신규 (PlayerAnimator/EnemyAnimator 와 동일 패턴, Boss.OnDied 구독). SpawnManager `RaiseEnemyAttackAnim(enemyId, facingLeft)` + Boss `RaiseAttackAnim` 추가. BossPhaseManager 가 패턴 Execute 직전 보스 attack 트리거 발화. 근접형 적은 Attack 트리거 미사용 (접촉 데미지 자체가 시각 공격). § 6.1 "단일 프리팹 + SO swap" 패턴 명시 (`EnemyBase.prefab` 하나가 모든 적 변형 운용 — 프리팹 셋업 1회로 전체 적용). 인게임 검증 중 § 12.1 follow-up 식별: 공격 중 이동 lock / anti-overlap IsMoving 오토글 / 공격-발사 타이밍 매칭 (3건 보류) + Ranged 적 발사 시 facing 갱신 (즉시 fix ✅).
