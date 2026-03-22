using System;
using UnityEngine;
using Photon.Pun;
using SwDreams.Adapter.Skill;
using SwDreams.Presentation;

namespace SwDreams.Adapter.Entity.Player
{
    /// <summary>
    /// 플레이어 체력 내부 로직. 데미지/회복, 사망/부활 처리.
    /// IDamageable은 PlayerStub에서 유지 (외부 호환). PlayerStub이 이 컴포넌트에 위임.
    ///
    /// [Phase 7 리팩토링] Step 2-1: PlayerStub에서 분리.
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class PlayerHealth : MonoBehaviourPun
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

        // ===== 데미지 (PlayerStub.TakeDamage에서 호출) =====

        /// <summary>호스트에서 호출. RPC로 전파.</summary>
        public void ApplyDamage(int damage)
        {
            if (!IsAlive) return;
            photonView.RPC(nameof(RPC_TakeDamage), RpcTarget.All, damage);
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
                    var respawnMgr = SwDreams.Adapter.Manager.RespawnManager.Instance;
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