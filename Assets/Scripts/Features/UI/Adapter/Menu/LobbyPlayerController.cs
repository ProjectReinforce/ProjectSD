using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using SwDreams.Features.Character.Adapter.Data;
using SwDreams.Shared.Managers;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SwDreams.Features.UI.Adapter.Menu
{
    /// <summary>
    /// 대기실 전용 플레이어 컨트롤러.
    ///
    /// 무엇: 대기실 월드 공간에 놓인 각 플레이어의 아바타 루트 스크립트.
    /// 왜:   인게임 PlayerStub/PlayerMovement는 GameState, PlayerStats, PlayerHealth 등과
    ///       강하게 결합되어 있어 대기실에서 그대로 쓰면 의존성/리스크가 크다.
    ///       대기실은 "캐릭터 선택을 시각적으로 확인하며 돌아다니는" 연출 공간이므로
    ///       전용 프리팹 + 최소 의존 컴포넌트로 분리한다.
    /// 어떻게: photonView.IsMine일 때만 WASD 입력으로 Rigidbody2D를 움직이고,
    ///       오너 플레이어의 characterId CustomProperty가 바뀌면 SpriteRenderer를 교체한다.
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class LobbyPlayerController : MonoBehaviourPunCallbacks, IPunInstantiateMagicCallback
    {
        [Header("이동")]
        [SerializeField] private float moveSpeed = 3f;

        [Header("애니메이션")]
        [Tooltip("sprite 의 기본 향. true=오른쪽, false=왼쪽. 좌/우 입력 시 flipX 분기 기준.")]
        [SerializeField] private bool defaultFacingRight = true;

        [Header("외관")]
        [Tooltip("CharacterData.portrait를 대기실 스프라이트로 사용. 프리팹 Inspector에서 연결.")]
        [SerializeField] private CharacterDatabase characterDB;

        [Tooltip("CharacterData.id 인덱스로 참조되는 대기실 전용 스프라이트 배열(optional). 비어 있으면 portrait 사용.")]
        [SerializeField] private Sprite[] characterSprites;

        private Rigidbody2D rb;
        private SpriteRenderer spriteRenderer;
        private Animator animator;
        private int appliedCharacterId = -1;

        // 리모트 velocity 추정용. PhotonTransformView 가 transform 만 동기화하므로 차분으로 속도 추정.
        private Vector3 lastRemotePos;
        private bool hasLastRemotePos;

        // 피벗 보정용 — PlayerAnimator 와 동일 패턴. SR 자식 transform.localPosition 시프트.
        private Transform spriteTransform;
        private Vector3 spriteBaseLocalPosition;
        private float pivotOffsetX;
        private bool? lastFlipState;

        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
        private static readonly int MoveXHash = Animator.StringToHash("MoveX");
        private static readonly int MoveYHash = Animator.StringToHash("MoveY");
        private const float MoveThreshold = 0.05f;

        private void Awake()
        {
            // [B8] 잘못된 씬(예: GameScene 결과창 시점에 다른 클라가 MenuScene 진입해
            // PhotonNetwork.Instantiate 한 LobbyPlayer 가 우리 GameScene 에 spawn) 에 spawn 되면
            // 자기 측에서 즉시 제거. 실제 MenuScene 에 진입 시 RaiseRefreshRequest 로
            // 재 spawn 받음 (LobbyPlayerSpawner.OnEvent).
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "MenuScene")
            {
                Destroy(gameObject);
                return;
            }

            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;

            // SpriteRenderer/Animator 는 own GO 또는 자식 — InChildren 으로 둘 다 커버 (자기 GO 도 포함).
            // includeInactive=true — 자식 GO 가 비활성으로 시작해도 잡도록.
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
            animator = GetComponentInChildren<Animator>(true);

            // 피벗 보정은 SR 이 자식 GO 에 있을 때만 활성. root 면 본체 transform 이 통째로 움직이므로 무력화.
            if (spriteRenderer != null && spriteRenderer.transform != transform)
            {
                spriteTransform = spriteRenderer.transform;
                spriteBaseLocalPosition = spriteTransform.localPosition;
            }
        }

        /// <summary>
        /// Photon이 네트워크 오브젝트를 완전히 준비한 직후(Owner 세팅 완료) 호출.
        /// Awake/OnEnable 시점에는 photonView.Owner가 null일 수 있으므로 여기서 초기 외관을 적용.
        /// </summary>
        public void OnPhotonInstantiate(Photon.Pun.PhotonMessageInfo info)
        {
            // 리모트 인스턴스는 PhotonTransformView가 transform.position을 직접 제어하므로
            // Dynamic Rigidbody2D와 충돌하면 떨림/스파이크가 날 수 있다. Kinematic이 안전.
            if (!photonView.IsMine && rb != null)
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
            }

            ApplyOwnerCharacter();
        }

        public override void OnEnable()
        {
            base.OnEnable();
            // Instantiate 이전이면 Owner가 없을 수 있어 no-op; OnPhotonInstantiate에서 처리.
            if (photonView != null && photonView.Owner != null)
            {
                ApplyOwnerCharacter();
            }
        }

        private void Update()
        {
            if (!photonView.IsMine)
            {
                // 리모트: 입력은 받지 않지만, 동기화된 위치 차분으로 velocity 추정해 같은 애니메이터 토글.
                UpdateRemoteAnimator();
                return;
            }

            var kb = Keyboard.current;
            if (kb == null)
            {
                rb.linearVelocity = Vector2.zero;
                return;
            }

            Vector2 input = Vector2.zero;
            if (kb.wKey.isPressed) input.y += 1f;
            if (kb.sKey.isPressed) input.y -= 1f;
            if (kb.dKey.isPressed) input.x += 1f;
            if (kb.aKey.isPressed) input.x -= 1f;
            input = input.normalized;

            rb.linearVelocity = input * moveSpeed;

            UpdateAnimatorParams(rb.linearVelocity);
        }

        /// <summary>
        /// 리모트 인스턴스용: PhotonTransformView 가 보간한 transform.position 을 프레임 차분해
        /// velocity 를 추정한 뒤 UpdateAnimatorParams 로 IsMoving / flipX 를 동일하게 갱신.
        /// </summary>
        private void UpdateRemoteAnimator()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;

            Vector3 cur = transform.position;
            Vector2 v = Vector2.zero;
            if (hasLastRemotePos)
            {
                float dt = Time.deltaTime;
                if (dt > 0f) v = ((Vector2)(cur - lastRemotePos)) / dt;
            }
            lastRemotePos = cur;
            hasLastRemotePos = true;

            UpdateAnimatorParams(v);
        }

        /// <summary>
        /// IsMoving + MoveX/Y 토글 + flipX. 본인(IsMine) 은 입력→rb.linearVelocity 로,
        /// 리모트는 위치 차분 추정값으로 호출. 양쪽 모두 같은 파라미터 셋 사용.
        /// </summary>
        private void UpdateAnimatorParams(Vector2 velocity)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;

            bool moving = velocity.sqrMagnitude > MoveThreshold * MoveThreshold;
            animator.SetBool(IsMovingHash, moving);

            if (moving)
            {
                Vector2 dir = velocity.normalized;
                animator.SetFloat(MoveXHash, dir.x);
                animator.SetFloat(MoveYHash, dir.y);

                // 좌우 flipX — 입력의 x 부호로만 판정 (위/아래만 누르면 마지막 facing 유지).
                if (Mathf.Abs(velocity.x) > 0.01f && spriteRenderer != null)
                {
                    bool flipped = defaultFacingRight ? velocity.x < 0f : velocity.x > 0f;
                    spriteRenderer.flipX = flipped;
                    ApplyPivotOffset(flipped);
                }
            }
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

        // ===================================================================
        // 캐릭터 변경 감지 (CustomProperty)
        // ===================================================================

        public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
        {
            if (photonView == null || photonView.Owner == null) return;
            if (targetPlayer == null || targetPlayer.ActorNumber != photonView.Owner.ActorNumber) return;

            if (changedProps != null && changedProps.ContainsKey(NetworkManager.CharacterIdKey))
            {
                ApplyOwnerCharacter();
            }
        }

        private void ApplyOwnerCharacter()
        {
            if (photonView == null || photonView.Owner == null) return;

            if (NetworkManager.Instance == null) return;
            if (!NetworkManager.Instance.TryGetCharacterId(photonView.Owner, out int characterId)) return;
            if (characterId == appliedCharacterId) return;

            appliedCharacterId = characterId;

            Sprite next = ResolveSprite(characterId);
            if (next != null && spriteRenderer != null)
            {
                spriteRenderer.sprite = next;
            }

            // CharacterData 주입 — DB 가 있을 때만.
            if (characterDB != null)
            {
                var data = characterDB.GetById(characterId);
                if (data != null)
                {
                    // 피벗 보정값 갱신 — 캐릭터 swap 시 매번 재적용.
                    pivotOffsetX = data.pivotOffsetX;
                    lastFlipState = null;
                    if (spriteTransform != null && spriteRenderer != null)
                        ApplyPivotOffset(spriteRenderer.flipX);

                    if (animator != null && data.animatorController != null)
                    {
                        animator.runtimeAnimatorController = data.animatorController;
                        // controller swap 후 stale state/trigger 정리. 캐릭터 변경 직후 1프레임 잔류 방지.
                        animator.Rebind();
                    }
                }
            }
        }

        private Sprite ResolveSprite(int characterId)
        {
            if (characterSprites != null && characterSprites.Length > 0
                && characterId >= 0 && characterId < characterSprites.Length
                && characterSprites[characterId] != null)
            {
                return characterSprites[characterId];
            }

            if (characterDB == null) return null;

            var data = characterDB.GetById(characterId);
            return data != null ? data.portrait : null;
        }
    }
}
