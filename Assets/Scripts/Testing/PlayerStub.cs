using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;
using SwDreams.Domain.Interfaces;
using SwDreams.Adapter.Skill;
using SwDreams.Data;
using SwDreams.Adapter.Manager;
using SwDreams.Presentation;

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
        private SkillManager skillManager;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            CurrentHP = maxHP;
            gameObject.tag = "Player";

            skillManager = GetComponentInChildren<SkillManager>();
        }

        private void Start()
        {
            // 모든 클라이언트에서 모든 플레이어의 스킬 실행.
            // 투사체는 로컬 렌더링, 데미지는 호스트만 처리.
            if (startingSkillData != null && skillManager != null)
            {
                skillManager.AcquireSkill(startingSkillData);
                Debug.Log($"[PlayerStub] 시작 스킬 획득: {startingSkillData.skillName}");
            }
            else
            {
                Debug.LogWarning("[PlayerStub] startingSkillData 또는 SkillManager 없음");
            }

            // 테스트용 코드
            // if (skillManager == null) return;

            // // 테스트: 시작 스킬 + 더미 스킬 여러 개
            // if (startingSkillData != null)
            //     skillManager.AcquireSkill(startingSkillData);

            // // 같은 스킬 다시 넣기 → 레벨업 되어야 함
            // skillManager.AcquireSkill(startingSkillData);

            // // 슬롯 상태 출력
            // skillManager.LogSlotStatus();

            // 패시브 테스트 (임시)
            var testStats = GetComponent<PlayerStats>();
            if (testStats != null)
            {
                testStats.RecalculateAll();
                Debug.Log($"[Test] MoveSpeed: {testStats.MoveSpeed}, ATK: {testStats.AttackMultiplier}");
            }

            // 로컬 플레이어만 LevelUpManager에 등록
            if (photonView.IsMine && LevelUpManager.Instance != null)
            {
                LevelUpManager.Instance.RegisterLocalPlayer(skillManager);
            }
        }

        private void Update()
        {
            if (!photonView.IsMine) return;

            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentState != GameManager.GameState.Playing)
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

            rb.linearVelocity = input * moveSpeed;

            if (PhotonNetwork.IsMasterClient && GameManager.Instance != null)
            {
                if (kb.lKey.wasPressedThisFrame)
                {
                    GameManager.Instance?.AddExp(9999);
                    Debug.Log("[Test] 강제 레벨업");
                }

                // K: 게임 강제 재개 (Paused → Playing)
                if (kb.kKey.wasPressedThisFrame)
                {
                    GameManager.Instance?.ChangeStateNetwork(GameManager.GameState.Playing);
                    if (UIManager.Instance != null)
                        UIManager.Instance.HideLevelUp();
                    Debug.Log("[Test] 강제 재개");
                }

                // J: 선택 스킵 (현재 선택지 중 첫 번째 자동 선택)
                if (kb.jKey.wasPressedThisFrame)
                {
                    if (LevelUpManager.Instance != null && LevelUpManager.Instance.IsLevelUpActive)
                    {
                        var choices = LevelUpManager.Instance.GetCurrentChoices();
                        if (choices != null && choices.Length > 0)
                        {
                            LevelUpManager.Instance.SubmitChoice(choices[0].skillId);
                            Debug.Log($"[Test] 자동 선택: {choices[0].skillName}");
                        }
                    }
                }
            }
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
    }
}
