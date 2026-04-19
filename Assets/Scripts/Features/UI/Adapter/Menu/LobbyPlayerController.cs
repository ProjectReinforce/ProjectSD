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
    [RequireComponent(typeof(SpriteRenderer))]
    public class LobbyPlayerController : MonoBehaviourPunCallbacks, IPunInstantiateMagicCallback
    {
        [Header("이동")]
        [SerializeField] private float moveSpeed = 3f;

        [Header("외관")]
        [Tooltip("CharacterData.portrait를 대기실 스프라이트로 사용. 프리팹 Inspector에서 연결.")]
        [SerializeField] private CharacterDatabase characterDB;

        [Tooltip("CharacterData.id 인덱스로 참조되는 대기실 전용 스프라이트 배열(optional). 비어 있으면 portrait 사용.")]
        [SerializeField] private Sprite[] characterSprites;

        private Rigidbody2D rb;
        private SpriteRenderer spriteRenderer;
        private int appliedCharacterId = -1;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;

            spriteRenderer = GetComponent<SpriteRenderer>();
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
            if (!photonView.IsMine) return;

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
