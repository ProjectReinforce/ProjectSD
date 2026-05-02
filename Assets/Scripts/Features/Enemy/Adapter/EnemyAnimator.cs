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
    ///   MoveX    (Float, 옵션 — Blend Tree 4방향용)
    ///   MoveY    (Float, 옵션)
    ///
    /// 셋업: Enemy 본체 GO 에 부착. Animator 는 자식 GO (SpriteRenderer 와 같은 곳) 에 부착.
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
        private static readonly int MoveXHash = Animator.StringToHash("MoveX");
        private static readonly int MoveYHash = Animator.StringToHash("MoveY");

        private Enemy boundEnemy;
        private Vector3 lastPosition;
        private bool hasLastPosition;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
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
            if (animator == null || data == null) return;
            if (data.animatorController == null) return; // 미설정 = 정적 sprite
            animator.runtimeAnimatorController = data.animatorController;
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
                    spriteRenderer.flipX = defaultFacingRight ? v.x < 0f : v.x > 0f;
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
