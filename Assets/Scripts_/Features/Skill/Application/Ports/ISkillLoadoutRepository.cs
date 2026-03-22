namespace Features.Skill.Application.Ports
{
    /// <summary>슬롯별 스킬 ID 매핑</summary>
    public readonly struct SkillLoadout
    {
        public string[] SlotSkillIds { get; }

        public SkillLoadout(string[] slotSkillIds)
        {
            SlotSkillIds = slotSkillIds;
        }
    }

    public interface ISkillLoadoutRepository
    {
        SkillLoadout Load();
    }
}
