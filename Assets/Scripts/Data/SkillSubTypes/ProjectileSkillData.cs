using UnityEngine;

namespace SwDreams.Data
{
    /// <summary>
    /// 투사체형 스킬 데이터.
    /// SkillData를 상속하며, CreateAssetMenu만 분리.
    /// 인스펙터 표시는 SkillDataEditor에서 effectType에 따라 필터링.
    ///
    /// 사용 스킬: 표창, 매직미사일, 부메랑, 회오리바람 + 진화 스킬들
    /// </summary>
    [CreateAssetMenu(fileName = "NewProjectileSkill", menuName = "SwDreams/Skill/Projectile")]
    public class ProjectileSkillData : SkillData { }
}
