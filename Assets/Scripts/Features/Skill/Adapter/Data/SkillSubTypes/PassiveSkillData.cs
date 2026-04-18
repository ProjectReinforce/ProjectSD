using UnityEngine;
using SwDreams.Features.Skill.Adapter.Data;

namespace SwDreams.Features.Skill.Adapter.Data
{
    /// <summary>
    /// 패시브 스킬 데이터.
    /// 사용 스킬: 13종 패시브 (투사체 속도 증가, 공격력 증가 등)
    /// </summary>
    [CreateAssetMenu(fileName = "NewPassiveSkill", menuName = "SwDreams/Skill/Passive")]
    public class PassiveSkillData : SkillData { }
}
