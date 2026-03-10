using UnityEngine;
using SwDreams.Domain.Interfaces;
using SwDreams.Adapter.Manager;

namespace SwDreams.Adapter.Entity
{
    /// <summary>
    /// 전체 슬로우 공격. IBossAttackPattern 구현.
    /// Phase 3 전용.
    ///
    /// 모든 플레이어 이동속도 감소. duration초 후 해제.
    /// 슬로우 적용/해제는 BossPhaseManager가 RPC로 처리.
    /// </summary>
    public class GlobalSlowAttack : IBossAttackPattern
    {
        private readonly float cooldown;
        private readonly float duration;
        private readonly float slowMultiplier; // 0.5 = 50% 감속

        public float Cooldown => cooldown;

        public GlobalSlowAttack(float cooldown, float duration, float slowMultiplier)
        {
            this.cooldown = cooldown;
            this.duration = duration;
            this.slowMultiplier = slowMultiplier;
        }

        public bool CanExecute(float timeSinceLastUse)
        {
            return timeSinceLastUse >= cooldown;
        }

        public void Execute(Transform bossTransform, Transform target)
        {
            // BossPhaseManager에 슬로우 요청
            BossPhaseManager.Instance?.ApplyGlobalSlow(duration, slowMultiplier);

            Debug.Log($"[GlobalSlowAttack] 전체 슬로우 ({slowMultiplier * 100}%, {duration}초)");
        }
    }
}