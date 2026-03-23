using System.Collections.Generic;
using Features.Skill.Domain;
using Shared.Kernel;

namespace Features.Skill.Application
{
    /// <summary>카탈로그 내 스킬을 순환 선택하는 로직.</summary>
    public sealed class SkillRotator
    {
        private readonly List<string> _skillIds;

        public SkillRotator(IReadOnlyList<string> skillIds)
        {
            _skillIds = new List<string>(skillIds);
        }

        public int Count => _skillIds.Count;

        public string GetNext(SkillBar bar, int slotIndex)
        {
            if (_skillIds.Count == 0)
                return null;

            var current = bar.GetSkill(slotIndex);
            if (current == null)
                return _skillIds[0];

            var currentId = current.Id.Value;
            var index = _skillIds.IndexOf(currentId);
            if (index < 0)
                return _skillIds[0];

            return _skillIds[(index + 1) % _skillIds.Count];
        }
    }
}
