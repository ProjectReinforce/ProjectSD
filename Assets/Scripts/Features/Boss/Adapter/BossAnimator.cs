using UnityEngine;
using Photon.Pun;
using SwDreams.Features.Boss.Adapter.Data;
using SwDreams.Shared.Managers;

namespace SwDreams.Features.Boss.Adapter
{
    /// <summary>
    /// 보스 애니메이션 핸들러. EnemyAnimator 와 동일 패턴이지만 Boss 가 IPoolable/Enemy 를 상속하지 않고
    /// BossPhase/페이즈별 공격 패턴을 따로 가지므로 별도 컴포넌트로 분리.
    ///
    /// 책임:
    /// - BossData.animatorController 주입 (Boss.Initialize / InitializeFromNetwork 시점)
    /// - 위치 차분으로 IsMoving + MoveX/MoveY + flipX + 피벗 보정 토글
    /// - Boss.OnDied 구독 → Die 트리거
    /// - BossPhaseManager 가 공격 패턴 Execute 시점에 Boss.RPC_TriggerAttack 으로 Attack 트리거 발화
    ///
    /// AnimatorController parameters (표준):
    ///   IsMoving (Bool)
    ///   Die      (Trigger)
    ///   Attack   (Trigger — 공격 패턴 시작 시점에 외부에서 발화)
    ///   MoveX    (Float, 옵션 — Blend Tree 4방향용)
    ///   MoveY    (Float, 옵션)
    ///
    /// 셋업: Boss 본체 GO 에 부착. Animator 는 자식 GO (SpriteRenderer 와 같은 곳) 에 부착.
    /// 자식 GO 분리 권장: SpriteRenderer 가 root 와 같은 GO 에 있으면 피벗 보정이 무력화됨.
    /// </summary>
    public class BossAnimator : MonoBehaviour
    {
        [Tooltip("자식 GO 의 Animator. 비워두면 GetComponentInChildren 으로 자동 탐색.")]
        [SerializeField] private Animator animator;

        [Tooltip("이동 판정 임계값 (위치 차분 / dt 의 sqrMagnitude).")]
        [SerializeField] private float moveThreshold = 0.01f;

        [Tooltip("자식 SpriteRenderer. 비워두면 자동 탐색. flipX 토글용.")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
        private static readonly int DieHash = Animator.StringToHash("Die");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int MoveXHash = Animator.StringToHash("MoveX");
        private static readonly int MoveYHash = Animator.StringToHash("MoveY");

        private Boss boundBoss;
        private Vector3 lastPosition;
        private bool hasLastPosition;
        private bool defaultFacingRight = true;

        // 피벗 보정용 — PlayerAnimator/EnemyAnimator 와 동일 컨벤션.
        private Transform spriteTransform;
        private Vector3 spriteBaseLocalPosition;
        private float pivotOffsetX;
        private bool? lastFlipState;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);

            if (spriteRenderer != null && spriteRenderer.transform != transform)
            {
                spriteTransform = spriteRenderer.transform;
                spriteBaseLocalPosition = spriteTransform.localPosition;
            }
        }

        private void OnDestroy()
        {
            Unbind();
        }

        // ===== 외부 진입점 =====

        /// <summary>
        /// Boss.Initialize / InitializeFromNetwork 직후 호출. OnDied 구독 + controller 주입.
        /// 보스는 풀링되지 않아 1회 Bind 로 충분 — 재사용 케이스 없음.
        /// </summary>
        public void Bind(Boss boss, BossData data)
        {
            Unbind();

            boundBoss = boss;
            if (boundBoss != null)
                boundBoss.OnDied += OnBossDied;

            ApplyData(data);

            lastPosition = transform.position;
            hasLastPosition = true;
        }

        private void Unbind()
        {
            if (boundBoss != null)
                boundBoss.OnDied -= OnBossDied;
            boundBoss = null;
        }

        private void ApplyData(BossData data)
        {
            if (data == null) return;

            // 피벗 보정값 + 기본 향 주입.
            pivotOffsetX = data.pivotOffsetX;
            defaultFacingRight = data.defaultFacingRight;
            lastFlipState = null;
            if (spriteTransform != null && spriteRenderer != null)
                ApplyPivotOffset(spriteRenderer.flipX);

            if (animator == null) return;
            if (data.animatorController == null) return; // 미설정 = 정적 sprite
            animator.runtimeAnimatorController = data.animatorController;

            // 페이즈 전환 시 다른 controller 로 swap 하지 않으므로 Rebind 는 보스 한정 미수행.
            // 추후 페이즈별 controller swap 도입 시 여기서 Rebind() 호출.
        }

        /// <summary>
        /// 외부에서 명시적으로 공격 트리거 발화. Boss.RPC_TriggerAttack 경로로 모든 클라가 호출.
        /// </summary>
        public void TriggerAttack()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            animator.SetTrigger(AttackHash);
        }

        // ===== Update — IsMoving + MoveX/Y + flipX + 피벗 =====

        private void Update()
        {
            if (animator == null) return;
            if (animator.runtimeAnimatorController == null) return;

            // BossFight 외 상태(예: GameClear) 에선 Die 애니가 진행되도록 정지하지 않음.
            // Paused 시에만 정지 (PlayerAnimator 와 동일 정책).
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

                if (Mathf.Abs(v.x) > 0.01f && spriteRenderer != null)
                {
                    bool flipped = defaultFacingRight ? v.x < 0f : v.x > 0f;
                    spriteRenderer.flipX = flipped;
                    ApplyPivotOffset(flipped);
                }
            }
        }

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

        // ===== Boss 이벤트 핸들러 =====

        private void OnBossDied()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            animator.SetTrigger(DieHash);
        }
    }
}
