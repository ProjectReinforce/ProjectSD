using Features.Skill.Application.Ports;
using UnityEngine;

namespace Features.Skill.Infrastructure
{
    public sealed class SkillLoadoutRepository : ISkillLoadoutRepository
    {
        private readonly SkillLoadoutData _data;

        public SkillLoadoutRepository(SkillLoadoutData data)
        {
            _data = data;
        }

        public SkillLoadout Load()
        {
            if (_data == null || _data.SlotSkillIds == null)
            {
                Debug.LogError("[SkillLoadoutRepository] LoadoutData is missing.");
                return new SkillLoadout(new string[0]);
            }

            return new SkillLoadout(_data.SlotSkillIds);
        }
    }
}
