using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;
using SwDreams.Adapter.Manager;
using SwDreams.Shared.Managers;
using SwDreams.Adapter.Skill;

namespace SwDreams.Adapter.Entity.Player
{
    /// <summary>
    /// 플레이어 이동 처리. WASD 입력 + Rigidbody2D 제어 + 슬로우.
    ///
    /// [Phase 7 리팩토링] Step 2-2: PlayerStub에서 분리.
    ///
    /// 프리팹 구성: PlayerStub(또는 Player)와 같은 오브젝트에 부착.
    /// PhotonView 필요 (IsMine 체크).
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerMovement : MonoBehaviourPun
    {
        [Header("이동")]
        [SerializeField] private float moveSpeed = 0.8f;

        private Rigidbody2D rb;
        private float slowMultiplier = 1f;

        // 외부 참조: 사망 상태 체크용
        private PlayerHealth playerHealth;
        // PlayerStats 연동 (MoveSpeed 동기화)
        private PlayerStats playerStats;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }

        /// <summary>
        /// 의존 컴포넌트 바인딩. PlayerStub.Start()에서 호출.
        /// </summary>
        public void Bind(PlayerHealth health, PlayerStats stats)
        {
            playerHealth = health;

            if (playerStats != null)
                playerStats.OnStatsChanged -= OnStatsChanged;

            playerStats = stats;
            if (playerStats != null)
                playerStats.OnStatsChanged += OnStatsChanged;
        }

        /// <summary>캐릭터 base 이동속도 적용. PlayerInitializer에서 호출.</summary>
        public void SetMoveSpeed(float speed)
        {
            moveSpeed = speed;
        }

        private void OnDestroy()
        {
            if (playerStats != null)
                playerStats.OnStatsChanged -= OnStatsChanged;
        }

        private void Update()
        {
            if (!photonView.IsMine) return;

            // 사망 시 정지
            if (playerHealth != null && playerHealth.IsDead)
            {
                rb.linearVelocity = Vector2.zero;
                return;
            }

            // 게임 상태 체크
            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentState != GameManager.GameState.Playing &&
                GameManager.Instance.CurrentState != GameManager.GameState.BossFight)
            {
                rb.linearVelocity = Vector2.zero;
                return;
            }

            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            Vector2 input = Vector2.zero;
            if (kb.wKey.isPressed) input.y += 1f;
            if (kb.sKey.isPressed) input.y -= 1f;
            if (kb.dKey.isPressed) input.x += 1f;
            if (kb.aKey.isPressed) input.x -= 1f;
            input = input.normalized;

            rb.linearVelocity = input * moveSpeed * slowMultiplier;
        }

        // ===== 슬로우 =====

        /// <summary>
        /// 이동속도 배율 적용. BossPhaseManager에서 호출.
        /// </summary>
        public void ApplySlow(float multiplier)
        {
            slowMultiplier = Mathf.Clamp01(multiplier);
            Debug.Log($"[PlayerMovement] 슬로우 적용: {multiplier * 100}%");
        }

        public void RemoveSlow()
        {
            slowMultiplier = 1f;
            Debug.Log("[PlayerMovement] 슬로우 해제");
        }

        // ===== PlayerStats 연동 =====

        private void OnStatsChanged()
        {
            if (playerStats != null)
                moveSpeed = playerStats.MoveSpeed;
        }
    }
}