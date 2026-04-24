namespace SwDreams.Features.Progression.Domain
{
    /// <summary>
    /// 레벨업 선택지 패널의 종류. RPC 에 int 로 실려 전송된다.
    /// 값 변경 시 기존 RPC 버퍼 호환성 깨짐 주의.
    /// </summary>
    public enum ChoicePanelKind
    {
        /// <summary>일반 스킬 선택지.</summary>
        Skill = 0,

        /// <summary>혼돈 스킬 선택지 (레벨 10/20/30 등).</summary>
        Chaos = 1,

        /// <summary>능력치 부스트 선택지. 스킬 풀 고갈(= "만렙") 시 또는 퀘스트 보상.</summary>
        StatBoost = 2,
    }
}
