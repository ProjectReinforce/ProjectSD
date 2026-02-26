using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;
using SwDreams.Domain.Interfaces;
using SwDreams.Adapter.Skill;
using SwDreams.Data;

namespace SwDreams.Testing
{
    /// <summary>
    /// Phase 1 완료 전까지 사용하는 임시 플레이어.
    /// WASD 이동 + Photon 동기화 + IDamageable + 스킬 발동.
    /// 
    /// 프리팹 구성:
    /// - PlayerStub (이 스크립트)
    /// - PhotonView + PhotonTransformView
    /// - Rigidbody2D (Gravity 0, Freeze Rotation Z)
    /// - CircleCollider2D (isTrigger = false, Player 레이어)
    /// - SpriteRenderer
    /// - 자식 오브젝트 "SkillSlot": Skill + ProjectileEffect
    ///     → ProjectileEffect의 projectilePrefab에 Projectile 프리팹 연결
    /// 
    /// Resources 폴더에 "PlayerStub"으로 저장.
    /// Tag: "Player"
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class PlayerStub : MonoBehaviourPun, IDamageable
    {
        [Header("스탯")]
        [SerializeField] private int maxHP = 100;
        [SerializeField] private float moveSpeed = 5f;

        [Header("스킬 (테스트용)")]
        [SerializeField] private SkillData startingSkillData;

        public int CurrentHP { get; private set; }
        public int MaxHP => maxHP;
        public bool IsAlive => CurrentHP > 0;

        public event Action<int, int> OnHealthChanged;
        public event Action OnDied;

        private Rigidbody2D rb;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            CurrentHP = maxHP;
            gameObject.tag = "Player";
        }

        private void Start()
        {
            // 모든 클라이언트에서 모든 플레이어의 스킬 실행.
            // 투사체는 로컬 렌더링, 데미지는 호스트만 처리.
            ActivateSkills();
        }

        private void Update()
        {
            if (!photonView.IsMine) return;

            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            Vector2 input = Vector2.zero;
            if (kb.wKey.isPressed) input.y += 1f;
            if (kb.sKey.isPressed) input.y -= 1f;
            if (kb.dKey.isPressed) input.x += 1f;
            if (kb.aKey.isPressed) input.x -= 1f;
            input = input.normalized;

            rb.linearVelocity = input * moveSpeed;
        }

        /// <summary>
        /// 호스트에서 호출. 해당 플레이어에게 RPC로 데미지 전달.
        /// </summary>
        public void TakeDamage(int damage)
        {
            if (!IsAlive) return;

            // 호스트가 판정 → 해당 플레이어의 모든 클라이언트에 동기화
            photonView.RPC(nameof(RPC_TakeDamage), RpcTarget.All, damage);
        }

        [PunRPC]
        private void RPC_TakeDamage(int damage)
        {
            if (!IsAlive) return;

            CurrentHP = Mathf.Max(0, CurrentHP - damage);
            OnHealthChanged?.Invoke(CurrentHP, MaxHP);
            Debug.Log($"[PlayerStub] HP: {CurrentHP}/{MaxHP}");

            if (!IsAlive)
            {
                OnDied?.Invoke();
                Debug.Log("[PlayerStub] 사망!");
            }
        }

        /// <summary>
        /// 로컬 플레이어의 스킬 활성화.
        /// 자식에 있는 Skill 컴포넌트를 찾아서 Activate.
        /// </summary>
        private void ActivateSkills()
        {
            var skill = GetComponentInChildren<Skill>(true);
            if (skill != null && startingSkillData != null)
            {
                skill.Activate(startingSkillData);
                Debug.Log($"[PlayerStub] 스킬 활성화: {startingSkillData.skillName}");
            }
        }

        /// <summary>
        /// 원격 플레이어의 스킬 비활성화.
        /// 투사체는 로컬 전용이라 원격에서는 발동 안 함.
        /// </summary>
        private void DeactivateSkills()
        {
            var skill = GetComponentInChildren<Skill>(true);
            if (skill != null)
                skill.Deactivate();
        }
    }
}
