using System;
using SwDreams.Features.UI.Presentation;
using SwDreams.Features.Character.Adapter;
using SwDreams.Features.Skill.Adapter.TriggerEffects;
using UnityEngine;
using Photon.Pun;
using SwDreams.Features.Skill.Adapter;
using SwDreams.Shared.Domain.Interfaces;
using SwDreams.Shared.Managers;

namespace SwDreams.Features.Character.Adapter
{
    /// <summary>
    /// 플레이어 체력 내부 로직. 데미지/회복, 사망/부활 처리.
    /// IDamageable은 PlayerStub에서 유지 (외부 호환). PlayerStub이 이 컴포넌트에 위임.
    /// IHealable 은 Pickup Feature 가 PlayerHealth 를 직접 참조하지 않고 회복 가능하게 함.
    ///
    /// [Phase 7 리팩토링] Step 2-1: PlayerStub에서 분리.
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class PlayerHealth : MonoBehaviourPun, IHealable
    {
        [Header("체력")]
        [SerializeField] private int maxHP = 100;

        public int CurrentHP { get; private set; }
        public int MaxHP => maxHP;
        public bool IsAlive => CurrentHP > 0;
        public bool IsDead { get; private set; } = false;

        // 이벤트
        public event Action<int, int> OnHealthChanged;  // current, max
        public event Action OnDied;
        public event Action OnRespawned;
        /// <summary>피격 시 발생. PlayerVisual에서 구독.</summary>
        public event Action<int> OnHit;
        /// <summary>사망/부활 비주얼 변경. PlayerVisual에서 구독.</summary>
        public event Action<bool> OnDeadStateChanged;

        // PlayerStats 연동
        private PlayerStats playerStats;

        // R2 자연회복: 호스트 측 누적기 (HP 자체는 int 유지, 누적값만 float).
        // 1.0 이상 차면 정수 부분을 Heal RPC 로 송신하고 누적값에서 차감.
        private float hpRegenAccumulator;

        // R7 i-frame: 호스트 측 타이머. 0 보다 크면 ApplyDamage 무시.
        // 비주얼 깜빡임은 OnHit 이벤트에서 PlayerVisual 이 IFrameDuration 만큼 처리.
        private float iFrameTimer;

        private void Awake()
        {
            CurrentHP = maxHP;
        }

        /// <summary>캐릭터 base MaxHP 적용. PlayerStub.Initialize()에서 호출.</summary>
        public void SetMaxHP(int newMaxHP)
        {
            maxHP = newMaxHP;
            CurrentHP = maxHP;
        }

        /// <summary>PlayerStats 변경 구독. PlayerStub.Start()에서 호출.</summary>
        public void BindToStats(PlayerStats stats)
        {
            if (playerStats != null)
                playerStats.OnStatsChanged -= OnPlayerStatsChanged;

            playerStats = stats;
            if (playerStats != null)
                playerStats.OnStatsChanged += OnPlayerStatsChanged;
        }

        private void OnDestroy()
        {
            if (playerStats != null)
                playerStats.OnStatsChanged -= OnPlayerStatsChanged;
        }

        private void Update()
        {
            // GameState 가드 — 일시정지/메뉴 중 자연회복·i-frame 정지.
            var gm = GameManager.Instance;
            if (gm != null &&
                gm.CurrentState != GameManager.GameState.Playing &&
                gm.CurrentState != GameManager.GameState.BossFight)
                return;

            // R7 i-frame 카운트다운 (호스트만 실제 가드를 의미하므로 호스트 측 감소).
            if (PhotonNetwork.IsMasterClient && iFrameTimer > 0f)
            {
                iFrameTimer -= Time.deltaTime;
                if (iFrameTimer < 0f) iFrameTimer = 0f;
            }

            // R2 자연회복: 호스트만 누적. 1.0 이상 차면 정수 부분 Heal.
            if (PhotonNetwork.IsMasterClient && IsAlive && playerStats != null)
            {
                float regenPerSec = playerStats.HpRegen;
                if (regenPerSec > 0f && CurrentHP < MaxHP)
                {
                    hpRegenAccumulator += regenPerSec * Time.deltaTime;
                    if (hpRegenAccumulator >= 1f)
                    {
                        int healAmount = Mathf.FloorToInt(hpRegenAccumulator);
                        hpRegenAccumulator -= healAmount;

                        Heal(healAmount);
                    }
                }
                else
                {
                    hpRegenAccumulator = 0f;
                }
            }
        }

        // ===== 데미지 (PlayerStub.TakeDamage에서 호출) =====

        /// <summary>호스트에서 호출. 방어력 차감 + i-frame 가드 후 RPC로 전파.</summary>
        public void ApplyDamage(int damage)
        {
            // 데미지 권위 = 호스트. 클라가 직접 호출해도 RPC_TakeDamage(All) 가 N중복 디버프를
            // 일으킬 수 있으므로 진입부에서 호스트 가드.
            if (!PhotonNetwork.IsMasterClient) return;

            if (!IsAlive) return;
            if (damage <= 0) return;

            // R7 i-frame: 무적 시간 중이면 데미지 무시.
            if (iFrameTimer > 0f) return;

            // R1 방어력: PlayerStats.DefenseMultiplier 를 "받는 데미지 배율" 로 해석.
            // 1.0 = 그대로, 0.95 = 95% 받음. RegisterPassive 에서 부호 반전됐으므로 양수 입력이 감소를 의미.
            float defMul = playerStats != null ? playerStats.DefenseMultiplier : 1f;
            int finalDamage = Mathf.Max(1, Mathf.FloorToInt(damage * defMul));

            // i-frame 시작 (호스트만 — 다음 ApplyDamage 호출이 막힘).
            float iFrame = playerStats != null ? playerStats.IFrameDuration : 0f;
            if (iFrame > 0f) iFrameTimer = iFrame;

            photonView.RPC(nameof(RPC_TakeDamage), RpcTarget.All, finalDamage);
        }

        [PunRPC]
        private void RPC_TakeDamage(int damage)
        {
            if (!IsAlive) return;

            CurrentHP = Mathf.Clamp(CurrentHP - damage, 0, MaxHP);
            OnHealthChanged?.Invoke(CurrentHP, MaxHP);

            if (damage > 0)
            {
                DamagePopup.Spawn(transform.position, damage);
                HitEffect.Spawn(transform.position);
                OnHit?.Invoke(damage);
            }
            else if (damage < 0)
            {
                DamagePopup.Spawn(transform.position, -damage, isHeal: true);
            }

            if (!IsAlive && !IsDead)
            {
                IsDead = true;
                OnDied?.Invoke();
                OnDeadStateChanged?.Invoke(true);
                Debug.Log("[PlayerHealth] 사망!");

                if (PhotonNetwork.IsMasterClient)
                {
                    var respawnMgr = SwDreams.Features.Character.Adapter.RespawnManager.Instance;
                    if (respawnMgr != null)
                        respawnMgr.RequestRespawn(photonView.ViewID);
                }
            }
        }

        // ===== 부활 =====

        /// <summary>RespawnManager에서 호출. 모든 클라이언트에서 실행.</summary>
        public void Respawn(int newHP)
        {
            CurrentHP = Mathf.Clamp(newHP, 1, MaxHP);
            IsDead = false;
            OnHealthChanged?.Invoke(CurrentHP, MaxHP);
            OnDeadStateChanged?.Invoke(false);
            OnRespawned?.Invoke();
            Debug.Log($"[PlayerHealth] 부활! HP: {CurrentHP}/{MaxHP}");
        }

        // ===== 회복 =====

        /// <summary>
        /// 체력 회복. 호스트에서 호출 → RPC로 전파.
        /// HealSelfHandler, AreaZone(회복) 등에서 사용.
        /// </summary>
        public void Heal(int amount)
        {
            if (!IsAlive || amount <= 0) return;
            photonView.RPC(nameof(RPC_Heal), RpcTarget.All, amount);
        }

        [PunRPC]
        private void RPC_Heal(int amount)
        {
            if (!IsAlive) return;

            int before = CurrentHP;
            CurrentHP = Mathf.Clamp(CurrentHP + amount, 0, MaxHP);
            int healed = CurrentHP - before;

            if (healed > 0)
            {
                OnHealthChanged?.Invoke(CurrentHP, MaxHP);
                DamagePopup.Spawn(transform.position, healed, isHeal: true);
            }
        }

        // ===== PlayerStats 연동 =====

        private void OnPlayerStatsChanged()
        {
            if (playerStats == null) return;

            int newMaxHP = playerStats.MaxHP;
            if (newMaxHP != maxHP)
            {
                int oldMaxHP = maxHP;
                maxHP = newMaxHP;

                if (newMaxHP > oldMaxHP)
                    CurrentHP = Mathf.Min(CurrentHP + (newMaxHP - oldMaxHP), maxHP);
                else
                    CurrentHP = Mathf.Min(CurrentHP, maxHP);

                OnHealthChanged?.Invoke(CurrentHP, MaxHP);
                Debug.Log($"[PlayerHealth] MaxHP: {oldMaxHP} → {newMaxHP}, HP: {CurrentHP}/{MaxHP}");
            }
        }
    }
}