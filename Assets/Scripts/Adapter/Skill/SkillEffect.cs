using UnityEngine;

namespace SwDreams.Adapter.Skill
{
    /// <summary>
    /// 스킬 효과 추상 클래스.
    /// 
    /// 서브클래스:
    ///   ProjectileEffect → 표창, 매직미사일, 부메랑, 회오리바람 (Phase 2~5)
    ///   AreaEffect        → 개미지옥, 성역                      (Phase 5 ✔)
    ///   OrbitalEffect     → 장검                                (Phase 5 ✔)
    ///   PlacedEffect      → 자동포탑                             (Phase 5 ✔)
    ///   DebuffEffect      → 저주인형                             (Phase 5 ✔)
    ///
    /// Phase 5 Step 2 (예정):
    ///   LightningEffect  → 번개 (즉발 낙뢰, AreaEffect와 별도)
    ///   ProjectileEffect 확장 → Homing/Boomerang/SlowMoving 패턴
    ///
    /// 진화 스킬 (Phase 5 Step 4 예정):
    ///   각 Effect의 하위 클래스로 구현
    /// </summary>
    public abstract class SkillEffect : MonoBehaviour
    {
        public abstract void Execute(Skill skill);
    }
}
