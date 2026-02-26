using SwDreams.Domain.Formulas;
using SwDreams.Domain.ValueObjects;

namespace SwDreams.Application
{
    /// <summary>
    /// 데미지 유즈케이스. 순수 C#, Domain만 참조.
    /// 
    /// Phase 2: 기본 데미지 처리
    /// Phase 4: attackMul에 패시브 스킬 보정값 전달
    /// Phase 5: 혼돈 스킬 보정 추가
    /// </summary>
    public class DamageService
    {
        public DamageResult ProcessSkillAttack(int baseDamage,
            float attackMul = 1f, float defenseMul = 1f)
        {
            int damage = DamageFormula.Calculate(baseDamage, attackMul, defenseMul);
            return new DamageResult(damage);
        }

        public DamageResult ProcessContactDamage(int contactDamage,
            float defenseMul = 1f)
        {
            int damage = DamageFormula.CalculateContact(contactDamage, defenseMul);
            return new DamageResult(damage);
        }
    }
}
