using UnityEngine;

namespace SwDreams.Features.Skill.Adapter
{
    /// <summary>
    /// 치명타 판정 헬퍼. damage-formula.md § 4.3 / § 9 규약 준수.
    ///
    /// 호출 측 책임:
    ///   - 호스트 가드는 호출 측에서. 본 헬퍼는 Random.value 만 굴리므로 클라에서 호출하면 비결정적.
    ///   - 단일 적중 내 1회 판정 (동시 여러 소스도 1회만) — § 9.
    ///   - 체인 / 연쇄폭발 노드별 재판정 — 호출 측에서 노드마다 Roll 호출.
    ///   - DoT 부착 시점 1회 판정 → 결과 스냅샷 — 호출 측이 isCrit 보존 후 매 틱 적용.
    /// </summary>
    public static class CritJudgment
    {
        /// <summary>
        /// 치명타 판정 + 데미지 곱 산출.
        /// </summary>
        /// <param name="baseDamage">치명타 적용 전 데미지 (이미 ApplyAttackTo / DebuffMark / 0.8^n 등 다 적용된 후 값).</param>
        /// <param name="critChance">치명타 확률 (0~1). 0 이면 무조건 false.</param>
        /// <param name="critMultiplier">치명타 시 데미지 배율 (보통 1.5~).</param>
        /// <param name="isCrit">판정 결과.</param>
        /// <returns>치명타 시 baseDamage × critMultiplier (정수 라운드, 최소 1), 아니면 baseDamage 그대로.</returns>
        public static int Roll(int baseDamage, float critChance, float critMultiplier, out bool isCrit)
        {
            isCrit = critChance > 0f && Random.value < critChance;
            if (!isCrit) return baseDamage;
            int crit = Mathf.RoundToInt(baseDamage * critMultiplier);
            return Mathf.Max(1, crit);
        }

        /// <summary>
        /// 강제 치명타 (PlacedTurret alwaysCritical 등). 판정 없이 무조건 critMultiplier 적용.
        /// </summary>
        public static int Force(int baseDamage, float critMultiplier, out bool isCrit)
        {
            isCrit = true;
            int crit = Mathf.RoundToInt(baseDamage * critMultiplier);
            return Mathf.Max(1, crit);
        }
    }
}
