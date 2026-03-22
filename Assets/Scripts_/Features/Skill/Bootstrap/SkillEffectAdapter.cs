using Features.Skill.Presentation;
using UnityEngine;

namespace Features.Skill.Bootstrap
{
    public sealed class SkillEffectAdapter : ISkillEffectPort
    {
        private readonly SkillCatalog _catalog;

        public SkillEffectAdapter(SkillCatalog catalog)
        {
            _catalog = catalog;
        }

        public GameObject GetEffectPrefab(string skillId)
        {
            var data = _catalog.GetData(skillId);
            return data != null ? data.CastEffectPrefab : null;
        }

        public AudioClip GetCastSound(string skillId)
        {
            var data = _catalog.GetData(skillId);
            return data != null ? data.CastSound : null;
        }
    }
}
