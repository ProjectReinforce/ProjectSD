using UnityEngine;
using SwDreams.Features.Enemy.Adapter.Data;
using SwDreams.Shared.Managers;

namespace SwDreams.Features.Enemy.Adapter
{
    /// <summary>
    /// 적 애니메이션 핸들러. Enemy 본체(상태/HP/풀링)와 책임 분리.
    ///
    /// 책임:
    /// - EnemyData.animatorController 주입 (Enemy.Initialize 시점)
    /// - 위치 변화량 폴링으로 IsMoving + MoveX/MoveY 토글 (Rigidbody2D.linearVelocity 가
    ///   적의 ChaseMovement/SwarmMovement 등에서 일관 사용되지 않음 — transform 위치 차분이 안전)
    /// - Enemy.OnDied 구독 → Die 트리거
    /// - 풀 반환 시 Animator.Rebind() 로 state 초기화 (다음 스폰에 사망 state 잔류 방지)
    ///
    /// AnimatorController parameters (표준):
    ///   IsMoving (Bool)
    ///   Die      (Trigger)
    ///   Attack   (Trigger, Ranged 전용 — 호스트가 EnemyAttack.FireOnce 직전 SpawnManager 경유로 동기 발화)
    ///   MoveX    (Float, 옵션 — Blend Tree 4방향용)
    ///   MoveY    (Float, 옵션)
    ///
    /// 셋업: Enemy 본체 GO 에 부착. Animator 는 자식 GO (SpriteRenderer 와 같은 곳) 에 부착.
    /// 자식 GO 분리 권장: SpriteRenderer 가 root 와 같은 GO 에 있으면 피벗 보정이 무력화됨.
    /// </summary>
    public class EnemyAnimator : MonoBehaviour
    {
        [Tooltip("자식 GO 의 Animator. 비워두면 GetComponentInChildren 으로 자동 탐색.")]
        [SerializeField] private Animator animator;

        [Tooltip("이동 판정 임계값 (위치 차분 / dt 의 sqrMagnitude).")]
        [SerializeField] private float moveThreshold = 0.01f;

        [Tooltip("sprite 의 기본 향. true=오른쪽, false=왼쪽. 좌/우 이동 시 flipX 분기.")]
        [SerializeField] private bool defaultFacingRight = true;

        [Tooltip("자식 SpriteRenderer. 비워두면 자동 탐색. flipX 토글용.")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
        private static readonly int DieHash = Animator.StringToHash("Die");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int MoveXHash = Animator.StringToHash("MoveX");
        private static readonly int MoveYHash = Animator.StringToHash("MoveY");

        private Enemy boundEnemy;
        private Vector3 lastPosition;
        private bool hasLastPosition;

        // 피벗 보정용 — PlayerAnimator 와 동일 컨벤션. SR 자식 transform.localPosition 시프트.
        // SpriteRenderer 가 root 와 같은 GO 면 spriteTransform=null 로 두어 무력화 (root 이동 시 물리/네트워크 영향).
        private Transform spriteTransform;
        private Vector3 spriteBaseLocalPosition;
        private float pivotOffsetX;
        private bool? lastFlipState;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);

            // 피벗 보정은 SR 이 자식 GO 에 있을 때만 활성. root 면 본체 transform 이 통째로 움직이므로 무력화.
            if (spriteRenderer != null && spriteRenderer.transform != transform)
            {
                spriteTransform = spriteRenderer.transform;
                spriteBaseLocalPosition = spriteTransform.localPosition;
            }
        }

        // ===== 외부 진입점 =====

        /// <summary>
        /// Enemy.Initialize 직후 호출. Enemy 이벤트 구독 + controller 주입.
        /// 매 스폰마다 호출됨 (풀 재사용).
        /// </summary>
        public void Bind(Enemy enemy, EnemyData data)
        {
            Unbind();

            boundEnemy = enemy;
            if (boundEnemy != null)
                boundEnemy.OnDied += OnEnemyDied;

            ApplyData(data);

            lastPosition = transform.position;
            hasLastPosition = true;
        }

        private void Unbind()
        {
            if (boundEnemy != null)
                boundEnemy.OnDied -= OnEnemyDied;
            boundEnemy = null; // 명시 — 다음 Bind 전까지 stale 참조 잔류 방지.
        }

        private void ApplyData(EnemyData data)
        {
            if (data == null) return;

            // 피벗 보정값 주입 — animatorController 유무와 무관하게 적용 (정적 sprite 적도 flipX 사용).
            pivotOffsetX = data.pivotOffsetX;
            lastFlipState = null; // 다음 ApplyPivotOffset 호출에서 무조건 적용
            if (spriteTransform != null && spriteRenderer != null)
                ApplyPivotOffset(spriteRenderer.flipX);

            if (animator == null) return;
            if (data.animatorController == null) return; // 미설정 = 정적 sprite
            animator.runtimeAnimatorController = data.animatorController;
        }

        /// <summary>
        /// 외부에서 명시적으로 공격 트리거 발화. SpawnManager.RPC_TriggerEnemyAttack 경로로 모든 클라가 호출.
        /// runtimeAnimatorController 또는 Attack 파라미터가 없으면 SetTrigger 가 무시됨 (안전).
        /// </summary>
        public void TriggerAttack()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            animator.SetTrigger(AttackHash);
        }

        /// <summary>
        /// 외부에서 명시적으로 facing 갱신. Stationary Ranged 적이 이동 없이 사거리 안 플레이어를
        /// 바라봐야 하는 케이스. defaultFacingRight 컨벤션 그대로 적용 + 피벗 보정 동기.
        /// SpawnManager.RPC_TriggerEnemyAttack 가 모든 클라에서 호출.
        /// </summary>
        public void FaceDirection(bool facingLeft)
        {
            if (spriteRenderer == null) return;
            bool flipped = defaultFacingRight ? facingLeft : !facingLeft;
            spriteRenderer.flipX = flipped;
            ApplyPivotOffset(flipped);
        }

        /// <summary>
        /// PlayerAnimator.ApplyPivotOffset 와 동일 컨벤션. flip 상태 변화 시에만 갱신.
        /// </summary>
        private void ApplyPivotOffset(bool flipped)
        {
            if (spriteTransform == null) return;
            if (lastFlipState.HasValue && lastFlipState.Value == flipped) return;
            lastFlipState = flipped;

            if (Mathf.Approximately(pivotOffsetX, 0f))
            {
                spriteTransform.localPosition = spriteBaseLocalPosition;
                return;
            }

            var p = spriteBaseLocalPosition;
            p.x += flipped ? -pivotOffsetX : +pivotOffsetX;
            spriteTransform.localPosition = p;
        }

        /// <summary>
        /// Enemy.OnReturnToPool 에서 호출. Animator state 초기화 (사망 state 잔류 방지).
        ///
        /// **호출 순서 의존성**: 반드시 `gameObject.SetActive(false)` 보다 먼저 호출되어야 함.
        /// `Animator.Rebind()` 는 GO 비활성 상태에서 호출 시 Unity 가 무시 + 경고.
        /// 현재 Enemy.OnReturnToPool 이 이 순서 지킴.
        /// </summary>
        public void OnReturnToPool()
        {
            Unbind();
            hasLastPosition = false;
            if (animator != null && animator.runtimeAnimatorController != null)
                animator.Rebind();
        }

        // ===== Update — 위치 차분으로 IsMoving 판정 =====

        private void Update()
        {
            if (animator == null) return;
            if (animator.runtimeAnimatorController == null) return;

            // 레벨업 / ESC 솔로 정지 (GameState=Paused) 시 Animator 도 정지.
            // PlayerAnimator 와 동일 정책. GameOver 는 Die 애니 진행되도록 정지 X.
            var gm = GameManager.Instance;
            if (gm != null && gm.CurrentState == GameManager.GameState.Paused)
            {
                animator.speed = 0f;
                return;
            }
            if (animator.speed != 1f) animator.speed = 1f;

            if (!hasLastPosition)
            {
                lastPosition = transform.position;
                hasLastPosition = true;
                return;
            }

            float dt = Time.deltaTime;
            Vector3 delta = transform.position - lastPosition;
            lastPosition = transform.position;

            if (dt <= 0f) return;

            Vector2 v = (Vector2)(delta / dt);
            bool moving = v.sqrMagnitude > moveThreshold * moveThreshold;

            animator.SetBool(IsMovingHash, moving);

            if (moving)
            {
                Vector2 dir = v.normalized;
                animator.SetFloat(MoveXHash, dir.x);
                animator.SetFloat(MoveYHash, dir.y);

                // 좌우 flipX — x 부호로만 판정.
                if (Mathf.Abs(v.x) > 0.01f && spriteRenderer != null)
                {
                    bool flipped = defaultFacingRight ? v.x < 0f : v.x > 0f;
                    spriteRenderer.flipX = flipped;
                    ApplyPivotOffset(flipped);
                }
            }
        }

        // ===== Enemy 이벤트 핸들러 =====

        private void OnEnemyDied()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            animator.SetTrigger(DieHash);
        }
    }
}
