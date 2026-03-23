using Features.Skill.Presentation;
using UnityEngine;

namespace Features.Skill.Bootstrap
{
    public sealed class SkillIconAdapter : ISkillIconPort
    {
        private readonly SkillCatalog _catalog;

        public SkillIconAdapter(SkillCatalog catalog)
        {
            _catalog = catalog;
        }

        public Sprite GetIcon(string skillId)
        {
            var data = _catalog.GetData(skillId);
            return data != null ? data.Icon : null;
        }
    }
}
