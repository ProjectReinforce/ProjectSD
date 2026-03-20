namespace SwDreams.Domain.Formulas
{
    /// <summary>
    /// 경험치 레벨업 테이블. 순수 C#.
    /// 목표: 15분에 레벨 30+ 도달.
    /// 공식: 5 + level × 4
    /// </summary>
    public static class LevelTable
    {
        public static int GetRequiredExp(int currentLevel)
        {
            return 5 + (currentLevel * 4);
        }

        /// <summary>
        /// 혼돈 스킬 선택 레벨인지 확인.
        /// NOTE: 실제 판정은 GameplayConfig.IsChaosLevel()을 사용.
        /// 이 메서드는 Config 접근이 불가한 Domain 레이어용 fallback.
        /// </summary>
        public static bool IsChaosSkillLevel(int level)
        {
            return level == 10 || level == 20 || level == 30;
        }
    }
}