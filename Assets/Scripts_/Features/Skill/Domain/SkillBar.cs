namespace Features.Skill.Domain
{
    public sealed class SkillBar
    {
        public const int SlotCount = 4;

        private readonly Skill[] _slots = new Skill[SlotCount];

        public bool Equip(int slotIndex, Skill skill)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount) return false;
            _slots[slotIndex] = skill;
            return true;
        }

        public Skill GetSkill(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount) return null;
            return _slots[slotIndex];
        }
    }
}
