namespace SwDreams.Domain.ValueObjects
{
    /// <summary>
    /// 투사체 발사 기준 방향.
    /// ProjectileEffect에서 SpreadPattern 적용 전 base direction을 결정.
    ///
    /// [Phase 7 리팩토링]
    /// </summary>
    public enum AimType
    {
        /// <summary>가장 가까운 적 방향 (기본).</summary>
        ClosestEnemy,

        /// <summary>플레이어 이동 방향. 정지 시 마지막 이동 방향.</summary>
        MoveDirection,

        /// <summary>이동 반대 방향. 회오리바람 등.</summary>
        ReverseMoveDirection,

        /// <summary>랜덤 방향.</summary>
        Random
    }
}