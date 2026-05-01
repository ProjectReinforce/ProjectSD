using UnityEngine;

namespace SwDreams.Features.Enemy.Adapter.Attack
{
    /// <summary>
    /// 공격 쿨다운 관리 컴포넌트. 누적 타이머(Tick) 기반.
    /// 원거리 적 외에 엘리트 특수 공격, 소환 주기 등에 재사용 가능.
    ///
    /// 사용 패턴:
    ///   cooldown.SetInterval(3f);
    ///   cooldown.MarkFiredNow();        // 초기 딜레이용 (첫 공격 전 interval 대기)
    ///   cooldown.Tick(Time.deltaTime);  // 호출자가 GameState 가드 통과 후 매 프레임 호출
    ///   if (cooldown.CanFire()) { ... cooldown.MarkFiredNow(); }
    ///
    /// Time.time 기반 대신 누적 방식인 이유:
    /// 게임이 GameState.LevelUp 등으로 일시정지될 때 timeScale 은 1로 유지되므로(멀티 특성)
    /// Time.time 은 계속 증가. 그러면 일시정지 동안 쿨다운이 흘러 재개 즉시 발사되는 버그 발생.
    /// 호출자가 GameState 를 가드한 다음에만 Tick 을 호출하면 일시정지 동안 쿨다운이 멈춘다.
    /// </summary>
    public class EnemyAttackCooldown : MonoBehaviour
    {
        private float interval;
        private float elapsed;

        public float Interval => interval;

        public void SetInterval(float interval)
        {
            this.interval = Mathf.Max(0f, interval);
        }

        /// <summary>다음 공격까지 interval 대기하도록 누적 타이머 리셋.</summary>
        public void MarkFiredNow()
        {
            elapsed = 0f;
        }

        /// <summary>이번 프레임 시간만큼 쿨다운 진행. 호출자가 GameState 가드 통과 후 호출.</summary>
        public void Tick(float deltaTime)
        {
            if (deltaTime > 0f)
                elapsed += deltaTime;
        }

        public bool CanFire()
        {
            return elapsed >= interval;
        }
    }
}
