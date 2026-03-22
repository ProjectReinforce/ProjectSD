using UnityEngine;

namespace Features.Skill.Infrastructure
{
    /// <summary>Inspector에서 기본 로드아웃을 설정하는 SO</summary>
    [CreateAssetMenu(fileName = "DefaultLoadout", menuName = "Skill/SkillLoadoutData")]
    public sealed class SkillLoadoutData : ScriptableObject
    {
        [SerializeField] private string[] slotSkillIds = new string[4];

        public string[] SlotSkillIds => slotSkillIds;
    }
}
