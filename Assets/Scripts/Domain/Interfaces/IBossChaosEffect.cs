namespace SwDreams.Domain.Interfaces
{
    /// <summary>
    /// 보스에게 적용되는 혼돈 스킬 효과 인터페이스.
    /// 플레이어용 IChaosEffect와 분리 (ISP).
    ///
    /// Lv15에서 미선택된 혼돈 스킬 1개가 보스에게 부여됨.
    /// BossChaosApplicator가 ChaosEffectType → IBossChaosEffect 매핑.
    /// </summary>
    public interface IBossChaosEffect
    {
        /// <summary>보스 스폰 시 효과 적용.</summary>
        void ApplyToBoss(SwDreams.Adapter.Entity.Boss boss);

        /// <summary>보스전 중 지속 효과 갱신 (호스트만).</summary>
        void OnBossUpdate(float deltaTime);

        /// <summary>보스 사망 시 정리.</summary>
        void Cleanup();
    }
}