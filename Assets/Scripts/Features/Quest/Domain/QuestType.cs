namespace SwDreams.Features.Quest.Domain
{
    /// <summary>
    /// 퀘스트 종류. 4유형 (docs/game-design/quest.md § 3.4).
    /// MVP: KillTarget 1종 우선. 나머지는 핸들러 stub.
    /// </summary>
    public enum QuestType
    {
        KillTarget,     // 지정된 적 N마리 처치
        KillInTime,     // 제한 시간 내 적 N마리 처치
        DodgeFalling,   // 낙하 공격 N회 모두 회피 (1회라도 맞으면 실패)
        Defend          // 목표물(NPC/구조물) 일정 시간 보호
    }
}
