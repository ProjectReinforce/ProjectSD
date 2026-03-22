using UnityEngine;

namespace Features.Skill.Bootstrap
{
    [CreateAssetMenu(fileName = "SkillCatalog", menuName = "Skill/SkillCatalogData")]
    public sealed class SkillCatalogData : ScriptableObject
    {
        [SerializeField] private SkillData[] skills;

        public SkillData[] Skills => skills;
    }
}
