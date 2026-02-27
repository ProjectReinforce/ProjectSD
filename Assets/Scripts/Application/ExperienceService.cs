using System;
using SwDreams.Domain.Formulas;

namespace SwDreams.Application
{
    /// <summary>
    /// 경험치/레벨업 유즈케이스. 순수 C#, Domain만 참조.
    /// 팀 전체 공유 경험치를 관리.
    /// </summary>
    public class ExperienceService
    {
        public int CurrentExp { get; private set; }
        public int CurrentLevel { get; private set; } = 1;

        /// <summary>
        /// 경험치 추가. 레벨업 발생 시 true 반환.
        /// 호스트에서만 호출.
        /// </summary>
        public int AddExp(int amount)
        {
            CurrentExp += amount;
            int levelUps = 0;

            while (CurrentExp >= LevelTable.GetRequiredExp(CurrentLevel))
            {
                CurrentExp -= LevelTable.GetRequiredExp(CurrentLevel);
                CurrentLevel++;
                levelUps++;
            }

            return levelUps;
        }

        public int GetRequiredExp()
        {
            return LevelTable.GetRequiredExp(CurrentLevel);
        }

        public bool IsChaosSkillLevel()
        {
            return LevelTable.IsChaosSkillLevel(CurrentLevel);
        }

        public void Reset()
        {
            CurrentExp = 0;
            CurrentLevel = 1;
        }
    }
}
