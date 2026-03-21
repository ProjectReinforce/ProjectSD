namespace Features.Skill.Domain
{
    public sealed class SkillBar
    {
        public const int SlotCount = 4;

        private readonly Skill[] _slots = new Skill[SlotCount];

        public void Equip(int slotIndex, Skill skill)
        {
            _slots[slotIndex] = skill;
        }

        public Skill GetSkill(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount) return null;
            return _slots[slotIndex];
        }
    }
}
