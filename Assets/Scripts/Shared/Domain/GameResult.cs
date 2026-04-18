namespace SwDreams.Shared.Domain
{
    /// <summary>
    /// 게임 결과 데이터. 순수 C# — Unity 의존 없음.
    /// ResultManager에서 생성, RPC로 전체 클라이언트에 전달.
    ///
    /// 단위 테스트 가능: 값 검증만 하면 됨.
    /// </summary>
    public class GameResult
    {
        public bool IsCleared { get; set; }
        public float PlayTime { get; set; }
        public int TeamLevel { get; set; }
        public int TotalKills { get; set; }
        public int TotalDeaths { get; set; }

        /// <summary>
        /// 보스에게 적용된 혼돈 스킬. 0이면 없음.
        /// ChaosEffectType int 값으로 전달 (enum 직접 참조 회피).
        /// </summary>
        public int BossChaosTypeId { get; set; }

        /// <summary>
        /// 각 플레이어의 빌드 요약. actorNumber → PlayerBuildData.
        /// ResultManager에서 수집 후 채움.
        /// </summary>
        public PlayerBuildData[] PlayerBuilds { get; set; }
    }

    /// <summary>
    /// 개별 플레이어 빌드 데이터.
    /// 결과 화면에서 "플레이어별 빌드 요약" 표시에 사용.
    /// </summary>
    public class PlayerBuildData
    {
        public int ActorNumber { get; set; }
        public string PlayerName { get; set; }
        public int CharacterId { get; set; }

        /// <summary>장착된 스킬 ID 배열 (액티브+패시브+진화).</summary>
        public int[] SkillIds { get; set; }

        /// <summary>각 스킬의 레벨 배열. SkillIds와 동일 인덱스.</summary>
        public int[] SkillLevels { get; set; }

        /// <summary>획득한 혼돈 스킬 타입 배열.</summary>
        public int[] ChaosTypeIds { get; set; }
    }
}
