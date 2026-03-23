namespace SwDreams.Adapter.Skill.Trajectories
{
    /// <summary>
    /// 투사체 궤적 행동 인터페이스.
    /// Projectile에 부착하여 이동 패턴을 결정.
    ///
    /// 구현체: StraightTrajectory, HomingTrajectory, BoomerangTrajectory 등.
    /// TrajectoryFactory에서 TrajectoryType으로 생성.
    ///
    /// [Phase 7 리팩토링] Step 3-7b
    /// </summary>
    public interface ITrajectoryBehavior
    {
        /// <summary>관통 여부. true면 적 적중 시 파괴되지 않음.</summary>
        bool Penetrates { get; }

        /// <summary>true면 기본 lifetime 체크를 건너뜀 (부메랑 등 자체 종료 로직).</summary>
        bool OverridesLifetime { get; }

        /// <summary>투사체 스폰 직후 호출. 초기 상태 설정.</summary>
        void Initialize(Projectile projectile);

        /// <summary>매 프레임 이동 처리. Projectile.MoveStep()에서 호출.</summary>
        void UpdateMovement(Projectile projectile, float deltaTime);

        /// <summary>풀 반환 시 상태 초기화.</summary>
        void Reset();
    }
}
