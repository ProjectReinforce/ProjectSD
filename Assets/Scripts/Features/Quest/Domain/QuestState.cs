namespace SwDreams.Features.Quest.Domain
{
    /// <summary>
    /// 퀘스트 거점 상태 머신.
    ///
    /// Idle → (전원 진입) → Waiting → (대기 시간 경과) → InProgress
    ///   InProgress → (목표 달성) → Completed
    ///   InProgress → (실패 조건) → Failed
    /// Waiting 도중 한 명이라도 이탈하면 Idle 로 리셋.
    /// </summary>
    public enum QuestState
    {
        Idle,
        Waiting,
        InProgress,
        Completed,
        Failed
    }
}
