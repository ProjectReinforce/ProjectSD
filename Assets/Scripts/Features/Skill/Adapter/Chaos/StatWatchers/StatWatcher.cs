namespace SwDreams.Features.Skill.Adapter.Chaos.StatWatchers
{
    /// <summary>
    /// 혼돈 효과의 "조건 변화 감지" 추상화.
    ///
    /// 책임 분리 (Phase 8-C):
    /// - watcher = 입력 신호 → 캐시 비교 → 변경 여부 반환.
    /// - 효과 적용 (modifier 등록 등) 은 ChaosSkillManager.RecalculateChaosModifiers 가
    ///   watcher 의 노출 상태를 읽어서 수행. 책임이 분리되어 있어 추후 handler 화 용이.
    ///
    /// 구체 구현:
    /// - <see cref="HpThresholdWatcher"/>  : Berserk — HP 임계 통과 토글
    /// - <see cref="TimerRampWatcher"/>    : AccelEngine — 시간 경과 비례 보너스
    /// - <see cref="NearbyCountWatcher"/>  : Unity — 근접 아군 수 (interval 폴링)
    /// </summary>
    public abstract class StatWatcher
    {
        /// <summary>매 프레임 호출. 캐시값과 비교해 변화 있으면 true 반환 → recalc 트리거.</summary>
        public abstract bool Tick();
    }
}
