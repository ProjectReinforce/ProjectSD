namespace SwDreams.Features.Skill.Domain.ValueObjects
{
    /// <summary>
    /// 투사체 궤적 패턴. Projectile에 ITrajectoryBehavior로 부착.
    /// [Phase 7 리팩토링] Step 3-7b
    /// </summary>
    public enum TrajectoryType
    {
        /// <summary>직선 이동 (기본).</summary>
        Straight,
        /// <summary>가장 가까운 적을 추적.</summary>
        Homing,
        /// <summary>전방 발사 → 감속 → 복귀. 관통.</summary>
        Boomerang,
        /// <summary>느린 전진 + 범위 흡인 + 틱 데미지. 관통.</summary>
        Tornado,
        /// <summary>고정 원점 중심 나선 확장 + 흡인 + 틱 데미지. 관통.</summary>
        Spiral,
        /// <summary>좌우 지그재그.</summary>
        Zigzag,
        /// <summary>사인파 곡선.</summary>
        SinWave
    }
}
