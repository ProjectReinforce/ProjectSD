namespace SwDreams.Features.Skill.Domain.ValueObjects
{
    /// <summary>
    /// 투사체 배치 패턴. 다중 투사체를 어떻게 배치할지 결정.
    /// ProjectileEffect에서 SpreadPatternFactory를 통해 사용.
    ///
    /// [Phase 7 리팩토링] Step 3-7a
    /// </summary>
    public enum SpreadPatternType
    {
        /// <summary>단일 방향 (count개가 같은 방향).</summary>
        Single,

        /// <summary>부채꼴. 기본 다중 발사 패턴.</summary>
        Fan,

        /// <summary>360도 균등 분배.</summary>
        Radial,

        /// <summary>랜덤 방향.</summary>
        Random
    }
}
