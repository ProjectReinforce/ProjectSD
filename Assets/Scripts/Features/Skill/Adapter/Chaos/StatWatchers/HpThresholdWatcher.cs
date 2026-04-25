using SwDreams.Shared.Domain.Interfaces;

namespace SwDreams.Features.Skill.Adapter.Chaos.StatWatchers
{
    /// <summary>
    /// HP 비율 임계 통과 여부를 추적. Berserk 등 "HP 낮을 때 발동" 패턴용.
    ///
    /// 활성 = currentHP &lt;= maxHP × thresholdRatio.
    /// 토글 발생 시 Tick() 가 true 1 회 반환 → ChaosSkillManager 가 recalc.
    ///
    /// IDamageable 은 provider 로 받아 매 Tick 최신 참조 조회 — ApplyChaos 가
    /// Start 보다 먼저 호출되어 첫 캐싱 시점에 null 인 케이스를 lazy 회복.
    /// </summary>
    public class HpThresholdWatcher : StatWatcher
    {
        private readonly System.Func<IDamageable> damageableProvider;
        private readonly System.Func<float> thresholdRatioProvider;

        private bool cachedActive;

        public HpThresholdWatcher(
            System.Func<IDamageable> damageableProvider,
            System.Func<float> thresholdRatioProvider)
        {
            this.damageableProvider = damageableProvider;
            this.thresholdRatioProvider = thresholdRatioProvider;
        }

        /// <summary>현재 임계 이하인지. RecalculateChaosModifiers 가 직접 조회.</summary>
        public bool IsActive => cachedActive;

        public override bool Tick()
        {
            var damageable = damageableProvider != null ? damageableProvider() : null;
            if (damageable == null) return false;

            float ratio = thresholdRatioProvider != null ? thresholdRatioProvider() : 0.3f;
            bool nowActive = damageable.CurrentHP <= damageable.MaxHP * ratio;

            if (nowActive != cachedActive)
            {
                cachedActive = nowActive;
                return true;
            }
            return false;
        }
    }
}
