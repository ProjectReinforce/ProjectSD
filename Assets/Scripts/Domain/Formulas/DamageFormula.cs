using System;

namespace SwDreams.Domain.Formulas
{
    /// <summary>
    /// 데미지 계산 공식. 순수 C#, Unity 의존성 없음.
    /// 단위 테스트 가능.
    /// 
    /// Phase 2: 기본 공식만
    /// Phase 4: attackMultiplier에 패시브 보정 반영
    /// Phase 5: 혼돈 스킬 보정 (유리대포 2배 등)
    /// </summary>
    public static class DamageFormula
    {
        public static int Calculate(int baseDamage,
            float attackMultiplier = 1f, float defenseMultiplier = 1f)
        {
            int result = (int)(baseDamage * attackMultiplier * defenseMultiplier);
            return Math.Max(1, result);
        }

        public static int CalculateContact(int contactDamage,
            float defenseMultiplier = 1f)
        {
            int result = (int)(contactDamage * defenseMultiplier);
            return Math.Max(1, result);
        }
    }
}
