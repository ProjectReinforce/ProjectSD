namespace SwDreams.Domain
{
    /// <summary>
    /// 보스 전투 페이즈.
    /// BossPhaseManager에서 체력 비율로 전환 판정.
    /// </summary>
    public enum BossPhase
    {
        None,       // 보스전 아님
        Phase1,     // 100%~60% : 추적 + 충격파
        Phase2,     // 60%~30%  : 속도 증가 + 원형 지대
        Phase3      // 30%~0%   : 광폭화 + 전체 슬로우
    }
}