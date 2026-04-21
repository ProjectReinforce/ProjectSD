using UnityEngine;

namespace SwDreams.Features.Enemy.Adapter.Attack
{
    /// <summary>
    /// 공격 쿨다운 관리 컴포넌트. Time.time 기반.
    /// 원거리 적 외에 엘리트 특수 공격, 소환 주기 등에 재사용 가능.
    ///
    /// 사용 패턴:
    ///   cooldown.SetInterval(3f);
    ///   cooldown.MarkFiredNow(); // 초기 딜레이용 (첫 공격 전 interval 대기)
    ///   if (cooldown.CanFire()) { ... cooldown.MarkFiredNow(); }
    /// </summary>
    public class EnemyAttackCooldown : MonoBehaviour
    {
        private float interval;
        private float lastFireTime;

        public float Interval => interval;

        public void SetInterval(float interval)
        {
            this.interval = Mathf.Max(0f, interval);
        }

        /// <summary>다음 공격까지 interval 대기하도록 현재 시각을 마지막 발사로 마킹.</summary>
        public void MarkFiredNow()
        {
            lastFireTime = Time.time;
        }

        public bool CanFire()
        {
            return Time.time - lastFireTime >= interval;
        }
    }
}
