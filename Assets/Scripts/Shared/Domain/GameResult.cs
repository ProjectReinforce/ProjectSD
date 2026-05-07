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

        // ===== 인-런 통계 (B-1a — run-statistics.md §3) =====

        /// <summary>이 플레이어의 자기 막타 카운트 (일반 적 + 보스 D13).</summary>
        public int RunKills { get; set; }

        /// <summary>이 플레이어의 자기 사망 횟수.</summary>
        public int RunDeaths { get; set; }

        /// <summary>이 플레이어가 가한 누적 데미지 (자기 발사 시점 누적).</summary>
        public float DamageDealt { get; set; }

        /// <summary>이 플레이어가 받은 누적 데미지.</summary>
        public float DamageTaken { get; set; }

        /// <summary>스킬별 발사 횟수. SkillIds와 동일 인덱스.</summary>
        public int[] SkillFireCounts { get; set; }

        /// <summary>스킬별 막타 카운트. SkillIds와 동일 인덱스.</summary>
        public int[] SkillKillCounts { get; set; }

        /// <summary>스킬별 누적 데미지. SkillIds와 동일 인덱스.</summary>
        public float[] SkillDamageDealt { get; set; }
    }
}
