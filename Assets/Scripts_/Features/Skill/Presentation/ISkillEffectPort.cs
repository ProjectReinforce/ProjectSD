using UnityEngine;

namespace Features.Skill.Presentation
{
    public interface ISkillEffectPort
    {
        GameObject GetEffectPrefab(string skillId);
        AudioClip GetCastSound(string skillId);
    }
}
