namespace SwDreams.Domain.Formulas
{
    /// <summary>
    /// 경험치 레벨업 테이블. 순수 C#.
    /// 목표: 10분에 레벨 18~22 도달 (Phase 7 밸런싱 시 조정).
    /// </summary>
    public static class LevelTable
    {
        public static int GetRequiredExp(int currentLevel)
        {
            return 10 + (currentLevel * 5);
        }

        /// <summary>
        /// 혼돈 스킬 선택 레벨인지 확인 (Lv.5, 10, 15).
        /// </summary>
        public static bool IsChaosSkillLevel(int level)
        {
            return level == 5 || level == 10 || level == 15;
        }
    }
}
