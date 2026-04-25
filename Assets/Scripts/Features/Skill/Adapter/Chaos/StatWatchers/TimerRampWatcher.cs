using UnityEngine;

namespace SwDreams.Features.Skill.Adapter.Chaos.StatWatchers
{
    /// <summary>
    /// 시간 경과 비례 보너스 추적. AccelEngine 등 "런 시작부터 N 초간 0 → max 선형 증가" 패턴용.
    ///
    /// 매 프레임 bonus 갱신 — 임계값(EPS) 이상 변하면 Tick() true 반환.
    /// 실 적용 시점에는 <see cref="CurrentBonus"/> 를 RecalculateChaosModifiers 가 직접 조회.
    /// </summary>
    public class TimerRampWatcher : StatWatcher
    {
        private const float EPS = 0.001f;

        private readonly System.Func<float> timeProvider;
        private readonly System.Func<float> maxValueProvider;
        private readonly System.Func<float> rampDurationProvider;

        // 초기값 -1 = 첫 Tick 에 무조건 변경 통지 (cachedBonus=0 으로 시작하면 0초 시점에 토글 누락 가능).
        private float cachedBonus = -1f;

        public TimerRampWatcher(
            System.Func<float> timeProvider,
            System.Func<float> maxValueProvider,
            System.Func<float> rampDurationProvider)
        {
            this.timeProvider = timeProvider;
            this.maxValueProvider = maxValueProvider;
            this.rampDurationProvider = rampDurationProvider;
        }

        /// <summary>현재 램프 보너스값. Mathf.Lerp(0, max, t/duration) 결과.</summary>
        public float CurrentBonus => Mathf.Max(cachedBonus, 0f);

        public override bool Tick()
        {
            float t = timeProvider != null ? timeProvider() : 0f;
            float max = maxValueProvider != null ? maxValueProvider() : 0f;
            float dur = rampDurationProvider != null ? rampDurationProvider() : 600f;
            if (dur <= 0f) dur = 600f;

            float bonus = Mathf.Lerp(0f, max, t / dur);

            if (Mathf.Abs(bonus - cachedBonus) > EPS)
            {
                cachedBonus = bonus;
                return true;
            }
            return false;
        }
    }
}
