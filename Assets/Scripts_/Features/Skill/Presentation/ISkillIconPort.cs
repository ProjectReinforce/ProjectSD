using UnityEngine;

namespace Features.Skill.Presentation
{
    public interface ISkillIconPort
    {
        Sprite GetIcon(string skillId);
    }
}
