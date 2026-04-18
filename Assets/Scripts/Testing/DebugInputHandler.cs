#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;
using SwDreams.Adapter.Manager;
using SwDreams.Shared.Managers;
using SwDreams.Adapter.Skill;
using SwDreams.Presentation;

namespace SwDreams.Adapter.Entity.Player
{
    /// <summary>
    /// 디버그 입력 처리. 에디터/개발 빌드 전용.
    ///
    /// [Phase 7 리팩토링] Step 2-5: PlayerStub에서 분리.
    ///
    /// 키:
    ///   L: 강제 레벨업 (+100 EXP)
    ///   K: 게임 강제 재개
    ///   P: 스탯 확인
    ///   T: 플레이어 즉사
    ///   B: 보스 즉시 소환
    ///   H: 보스 HP 30%로 설정
    /// </summary>
    public class DebugInputHandler : MonoBehaviourPun
    {
        private PlayerHealth playerHealth;
        private SkillManager skillManager;

        private void Start()
        {
            playerHealth = GetComponent<PlayerHealth>();
            skillManager = GetComponentInChildren<SkillManager>();
        }

        private void Update()
        {
            if (!photonView.IsMine) return;
            if (!PhotonNetwork.IsMasterClient) return;
            if (GameManager.Instance == null) return;

            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            // L: 강제 레벨업
            if (kb.lKey.wasPressedThisFrame)
            {
                GameManager.Instance.AddExp(100);
                Debug.Log("[Debug] 강제 레벨업");
            }

            // K: 게임 강제 재개
            if (kb.kKey.wasPressedThisFrame)
            {
                GameManager.Instance.ChangeStateNetwork(GameManager.GameState.Playing);
                UIManager.Instance?.HideLevelUp();
                Debug.Log("[Debug] 강제 재개");
            }

            // P: 스탯 확인
            if (kb.pKey.wasPressedThisFrame)
            {
                var stats = GetComponent<PlayerStats>();
                if (stats != null)
                {
                    Debug.Log($"[Stats] ATK: {stats.AttackMultiplier:F2}, " +
                              $"MoveSpeed: {stats.MoveSpeed:F1}, " +
                              $"ProjSpeed: {stats.ProjectileSpeedBonus:F1}, " +
                              $"ProjCount: {stats.ProjectileCountBonus}");
                    Debug.Log($"[Stats Modifiers]\n{stats.GetModifierDebugString()}");
                }

                if (skillManager != null)
                    skillManager.LogSlotStatus();
            }

            // T: 플레이어 즉사
            if (kb.tKey.wasPressedThisFrame)
            {
                playerHealth?.ApplyDamage(9999);
                Debug.Log("[Debug] 강제 즉사");
            }

            // B: 보스 즉시 소환
            if (kb.bKey.wasPressedThisFrame)
            {
                BossSpawner.Instance?.DebugSpawnBoss();
                Debug.Log("[Debug] 보스 강제 소환");
            }

            // H: 보스 HP 30%
            if (kb.hKey.wasPressedThisFrame)
            {
                var boss = BossSpawner.Instance?.CurrentBoss;
                if (boss != null && boss.IsAlive)
                {
                    int targetHP = Mathf.RoundToInt(boss.MaxHP * 0.3f);
                    int dmg = boss.CurrentHP - targetHP;
                    if (dmg > 0) boss.TakeDamage(dmg);
                    Debug.Log($"[Debug] 보스 HP → 30% ({targetHP})");
                }
            }
        }
    }
}
#endif