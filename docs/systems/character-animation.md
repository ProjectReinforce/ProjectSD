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
Revive   (Trigger) Death → Idle (옵션, 클립 없으면 무시됨)
MoveX    (Float)   4방향 Blend Tree 용 (정규화 -1~1)
MoveY    (Float)   동상
```

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
    public void Bind(Enemy enemy, EnemyData data); // OnDied 구독 + controller 주입
    public void OnReturnToPool();                   // Animator.Rebind() — Enemy.OnReturnToPool 에서 호출
}
```

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

## 8. 풀링 함정 (적 전용)

`Enemy.OnReturnToPool` 호출 순서:
```
1. enemyAnimator.OnReturnToPool()  ← Animator.Rebind()
2. ... 기타 reset ...
3. gameObject.SetActive(false)     ← 마지막
```

**중요**: `Animator.Rebind()` 는 `gameObject.SetActive(false)` 이후 호출하면 Unity 가 무시 + 경고. 순서 절대 바꾸지 말 것 (`EnemyAnimator.cs:88` 주석에 명시).

## 9. 깨진 sprite reference 검증 도구

`Assets/Scripts/Editor/AnimationClipValidator.cs` — sanctum 같은 외부 에셋 import 시 일부 .anim 의 sprite reference 가 깨진 케이스 일괄 스캔.

사용:
1. Project 창에서 검사할 폴더 선택 (예: `Assets/FromStore/sanctum_pixel`)
2. 메뉴 `Tools → Validate AnimationClip Sprites (Selected Folder)`
3. Console 에 깨진 .anim 목록 + frame index 출력

판정 기준: `m_Sprite` curve 의 어느 한 frame 이라도 reference == null 이면 카운트.

**False positive 가능**: sanctum effect 클립이 의도적으로 마지막 frame 에 null 두는 패턴. 진짜 broken 클립만 재생 시 sprite=null 로 set 되어 캐릭터 안 보임 (Magician 테스트 케이스).

## 10. 점진 도입 (Phase)

### Phase 1: 코드 인프라 ✅ (2026-05-01)
SO 필드 + PlayerAnimator + EnemyAnimator + LobbyPlayerController 수정 + PlayerMovement.SetInputLocked + PlayerStub/Enemy 통합. SO 의 controller 가 비어있으면 정적 sprite (기존 동작 유지) — 점진 도입 안전.

### Phase 2: 캐릭터 4종 controller + Override (사용자 작업)
- 옵션 C (혼합 종족): hero / archer / wizard / scyther
- base controller 1개 + 캐릭터별 Override 4개
- 각 `CharacterData_*.asset` 의 `animatorController` 슬롯 연결

### Phase 3: 적 N종 controller + Override (사용자 작업)
- 적 패키지별 Override
- `EnemyData_*.asset` 의 `animatorController` 슬롯 연결
- 풀링 환경 검증 (Rebind 동작)

### Phase 4: 보스 별도 controller (14_big_monster_bundle)
보스는 단일 정면 + hit/dead/ready 등 상태 풍부 → 별도 base controller (`BossBase.controller`).

### Phase 5: 4방향 Blend Tree 마이그레이션
Idle/Walk state 를 Blend Tree 로 교체. MoveX/MoveY parameter 활용. flipX 로직 제거 가능.

## 11. 알려진 제약

- [ ] **부활 클립 미정** — sanctum 에 revive 전용 anim 없음. Stand 복귀 + 별도 ParticleSystem 으로 처리 예정 (사용자 부활 이펙트 결정 대기)
- [x] **원격 IsMoving (인게임 + 로비)** — PlayerAnimator/LobbyPlayerController 모두 IsMine 분기 + transform.position 프레임 차분으로 velocity 추정. PhotonTransformView 가 보간한 위치를 사용하므로 자연스러움 (RPC/직렬화 추가 없음).
- [ ] **PlayerStub 의 OnDestroy 미작성** — Health 람다 dangling 이론상 가능. 같은 GO 라 실무 영향 작음
- [ ] **Animator.runtimeAnimatorController = null 일 때 Update 비용** — 매 프레임 null 체크만 하지만 수백 마리 적 일 때 누적. 향후 `enabled = false` 토글 검토 (성능 nit)

## 12. 기존 코드 참조

- 신규 컴포넌트:
  - [PlayerAnimator.cs](../../Assets/Scripts/Features/Character/Adapter/PlayerAnimator.cs)
  - [EnemyAnimator.cs](../../Assets/Scripts/Features/Enemy/Adapter/EnemyAnimator.cs)
  - [AnimationClipValidator.cs](../../Assets/Scripts/Editor/AnimationClipValidator.cs)
- 수정된 컴포넌트:
  - [LobbyPlayerController.cs](../../Assets/Scripts/Features/UI/Adapter/Menu/LobbyPlayerController.cs)
  - [PlayerMovement.SetInputLocked](../../Assets/Scripts/Features/Character/Adapter/PlayerMovement.cs)
  - [PlayerStub.Initialize](../../Assets/Scripts/Features/Character/Adapter/PlayerStub.cs)
  - [Enemy.Initialize / OnReturnToPool](../../Assets/Scripts/Features/Enemy/Adapter/Enemy.cs)
  - [PlayerVisual.OnDeadStateChanged](../../Assets/Scripts/Features/Character/Adapter/PlayerVisual.cs) — alpha=0.3 제거
  - [ResultManager.RPC_ShowResult](../../Assets/Scripts/Shared/Managers/ResultManager.cs) — resultPanelDelay 적용
- SO 필드:
  - [CharacterData.animatorController](../../Assets/Scripts/Features/Character/Adapter/Data/CharacterData.cs)
  - [EnemyData.animatorController](../../Assets/Scripts/Features/Enemy/Adapter/Data/EnemyData.cs)
- 관련 시스템:
  - [in-game-menu.md](in-game-menu.md) — GameState.Paused 정책 SSOT
  - [waiting-room.md](waiting-room.md) — LobbyPlayer 외관 흐름
  - [network-sync.md](network-sync.md) — 적 풀링 + Dead Reckoning

## 13. 변경 이력

- 2026-05-02: 초안. Phase 1 코드 인프라 완료. base controller 패턴 + flipX + GameState.Paused 시 정지 + 풀링 Rebind + 결과창 지연 명세.
- 2026-05-03: § 7.1 피벗 보정 메커니즘 추가 (`CharacterData.pivotOffsetX` + PlayerStub/LobbyPlayer Visual 자식 분리). PlayerAnimator/LobbyPlayerController 양쪽 적용.
