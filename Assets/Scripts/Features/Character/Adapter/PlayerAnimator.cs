using UnityEngine;
using Photon.Pun;
using SwDreams.Features.Character.Adapter.Data;
using SwDreams.Shared.Managers;

namespace SwDreams.Features.Character.Adapter
{
    /// <summary>
    /// 플레이어 애니메이션 핸들러. PlayerVisual(피격 플래시·사망 alpha)과 책임 분리.
    ///
    /// 책임:
    /// - CharacterData.animatorController 주입 (캐릭터 base 적용 시점)
    /// - 본인(IsMine) 은 Rigidbody2D.linearVelocity 폴링, 리모트는 transform 위치 차분으로
    ///   velocity 를 추정해 IsMoving + MoveX/MoveY 토글 (PhotonTransformView 가 위치만 동기화하므로)
    /// - PlayerHealth.OnDeadStateChanged 구독 → Die / Revive 트리거
    ///
    /// AnimatorController parameters (표준):
    ///   IsMoving (Bool)  — Idle ↔ Walk
    ///   Die      (Trigger) — Death 진입
    ///   Revive   (Trigger) — 부활 시퀀스 시작 (옵션, 클립 없으면 무시됨)
    ///   MoveX    (Float)  — 4방향 Blend Tree 용 (옵션)
    ///   MoveY    (Float)  — 동상
    ///
    /// 셋업: Player 본체 GO 에 부착. Animator 는 자식 GO (SpriteRenderer 와 같은 곳) 에 부착.
    /// </summary>
    public class PlayerAnimator : MonoBehaviour
    {
        [Tooltip("자식 GO 의 Animator. 비워두면 GetComponentInChildren 으로 자동 탐색.")]
        [SerializeField] private Animator animator;

        [Tooltip("이동 판정 임계값 (linearVelocity.sqrMagnitude). 이보다 작으면 Idle.")]
        [SerializeField] private float moveThreshold = 0.05f;

        [Tooltip("sprite 의 기본 향. true=오른쪽, false=왼쪽. 좌/우 입력 시 flipX 분기 기준.")]
        [SerializeField] private bool defaultFacingRight = true;

        [Tooltip("자식 SpriteRenderer. 비워두면 GetComponentInChildren 으로 자동 탐색. flipX 토글용.")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
        private static readonly int DieHash = Animator.StringToHash("Die");
        private static readonly int ReviveHash = Animator.StringToHash("Revive");
        private static readonly int MoveXHash = Animator.StringToHash("MoveX");
        private static readonly int MoveYHash = Animator.StringToHash("MoveY");

        private Rigidbody2D rb;
        private PhotonView pv;
        private PlayerHealth boundHealth;

        // 리모트 velocity 추정용. PhotonTransformView 가 transform 만 동기화하므로 차분으로 속도 추정.
        private Vector3 lastRemotePos;
        private bool hasLastRemotePos;

        private void Awake()
        {
            // includeInactive=true — 자식 GO 가 비활성으로 시작해도 잡도록.
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
            rb = GetComponent<Rigidbody2D>();
            // PhotonView 없으면(솔로/테스트 씬) IsMine=true 로 간주 — 기존 rb 폴링.
            pv = GetComponent<PhotonView>();
        }

        private void OnDestroy()
        {
            Unbind();
        }

        // ===== 외부 진입점 =====

        /// <summary>
        /// PlayerHealth 이벤트 구독. PlayerStub.Start() 또는 PlayerInitializer 에서 호출.
        /// </summary>
        public void Bind(PlayerHealth health)
        {
            Unbind();
            boundHealth = health;
            if (boundHealth != null)
                boundHealth.OnDeadStateChanged += OnDeadStateChanged;
        }

        private void Unbind()
        {
            if (boundHealth != null)
                boundHealth.OnDeadStateChanged -= OnDeadStateChanged;
            boundHealth = null;
        }

        /// <summary>
        /// 캐릭터 base 적용 시 호출. CharacterData.animatorController 주입.
        /// PlayerInitializer.ApplyCharacterBase / PlayerStub.Initialize 등에서 호출.
        /// </summary>
        public void ApplyCharacter(CharacterData data)
        {
            if (animator == null || data == null) return;
            if (data.animatorController == null) return; // 미설정 = 정적 sprite (기존 동작)
            animator.runtimeAnimatorController = data.animatorController;

            // controller swap 후 stale state/trigger 정리:
            // - 같은 이름 parameter 는 값 유지 (IsMoving=true 잔류 가능)
            // - 다른 controller 가 SetTrigger(Die/Revive) 잔재를 갖고 있으면 즉시 발화 위험
            // Rebind() 가 가장 강한 정리 — 모든 state/parameter default 로.
            animator.Rebind();
        }

        // ===== Update — IsMoving + MoveX/Y =====

        private void Update()
        {
            if (animator == null) return;
            if (animator.runtimeAnimatorController == null) return;

            // 레벨업 / ESC 솔로 정지 (GameState=Paused) 시 Animator 도 정지.
            // timeScale 정책 X 라 Animator 가 자동으로 안 멈춤 — 명시 토글 필요.
            // GameOver/GameClear 는 Die 애니가 진행되어야 하므로 여기서 정지 X.
            var gm = GameManager.Instance;
            if (gm != null && gm.CurrentState == GameManager.GameState.Paused)
            {
                animator.speed = 0f;
                return;
            }
            if (animator.speed != 1f) animator.speed = 1f;

            Vector2 v = ResolveVelocity();
            bool moving = v.sqrMagnitude > moveThreshold * moveThreshold;

            animator.SetBool(IsMovingHash, moving);

            // MoveX/MoveY 는 Blend Tree 용. 정규화해서 [-1, 1] 로 — Blend Tree threshold 셋업 편의.
            // parameter 없는 controller 면 SetFloat 가 무시됨 (안전).
            if (moving)
            {
                Vector2 dir = v.normalized;
                animator.SetFloat(MoveXHash, dir.x);
                animator.SetFloat(MoveYHash, dir.y);

                // 좌우 flipX — x 부호로만 판정 (위/아래만 누르면 마지막 facing 유지).
                if (Mathf.Abs(v.x) > 0.01f && spriteRenderer != null)
                    spriteRenderer.flipX = defaultFacingRight ? v.x < 0f : v.x > 0f;
            }
        }

        /// <summary>
        /// 본인은 Rigidbody2D.linearVelocity, 리모트는 transform 위치 차분으로 속도 추정.
        /// PhotonTransformView 가 위치를 보간 적용하므로 차분 결과도 충분히 부드럽다.
        /// </summary>
        private Vector2 ResolveVelocity()
        {
            bool isLocal = pv == null || pv.IsMine;
            if (isLocal)
            {
                return rb != null ? rb.linearVelocity : Vector2.zero;
            }

            Vector3 cur = transform.position;
            Vector2 v = Vector2.zero;
            if (hasLastRemotePos)
            {
                float dt = Time.deltaTime;
                if (dt > 0f) v = ((Vector2)(cur - lastRemotePos)) / dt;
            }
            lastRemotePos = cur;
            hasLastRemotePos = true;
            return v;
        }

        // ===== Health 이벤트 핸들러 =====

        private void OnDeadStateChanged(bool dead)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            if (dead) animator.SetTrigger(DieHash);
            else animator.SetTrigger(ReviveHash);
        }

        /// <summary>
        /// 외부에서 명시적으로 부활 트리거 발화 (RespawnManager 의 부활 시퀀스용).
        /// OnDeadStateChanged(false) 와 동일하지만 Health 이벤트와 무관하게 호출 가능.
        /// </summary>
        public void TriggerRevive()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            animator.SetTrigger(ReviveHash);
        }
    }
}
