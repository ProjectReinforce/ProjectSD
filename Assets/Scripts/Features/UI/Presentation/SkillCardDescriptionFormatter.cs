using System.Text;
using SwDreams.Features.Skill.Adapter.Data;
using SwDreams.Shared.Domain.ValueObjects;

namespace SwDreams.Features.UI.Presentation
{
    /// <summary>
    /// 레벨업 카드 — 스킬별 / 등급별 수치 변화 라인 생성기.
    ///
    /// 책임 분리:
    /// - SO 의 <c>description</c> 필드는 플레이버 텍스트 (변경 없음).
    /// - 본 포맷터는 그 아래 자동 생성되는 "수치 라인" 만 담당.
    ///
    /// 출력 형식:
    ///   Active 미보유:        "데미지 15 / 쿨다운 1.5초"
    ///   Active 보유 Lv.3→4:   "데미지 22 → 26 / 쿨다운 1.3초 → 1.2초"
    ///   Passive 미보유:        "투사체 개수 +1"
    ///   Passive 보유 Lv.2→3:  "투사체 개수 +2 → +3"
    ///   Chaos (rarity 인덱싱): "공격력 ×3.0 / 최대 체력 ×0.5"
    ///
    /// SkillCardUI 가 호출. currentLevel = 0 → 미보유 (NEW), 1 이상 → "보유 후 다음 레벨" 표시.
    /// chaosRarity 는 <see cref="SkillType.Chaos"/> 에서만 의미 있음.
    /// </summary>
    public static class SkillCardDescriptionFormatter
    {
        private const string Separator = "\n";

        /// <summary>
        /// 수치 라인 생성. 빈 문자열 가능 (Chaos SO 미설정 / Passive bonusType=None 등).
        /// </summary>
        public static string FormatStats(SkillData data, int currentLevel, Rarity chaosRarity)
        {
            if (data == null) return string.Empty;

            switch (data.skillType)
            {
                case SkillType.Active:
                    return FormatActive(data, currentLevel);
                case SkillType.Passive:
                    return FormatPassive(data, currentLevel);
                case SkillType.Chaos:
                    return FormatChaos(data, chaosRarity);
                default:
                    return string.Empty;
            }
        }

        // ===== Active =====

        private static string FormatActive(SkillData data, int currentLevel)
        {
            var sb = new StringBuilder();

            // 데미지
            if (data.damagePerLevel != null && data.damagePerLevel.Length > 0)
            {
                if (currentLevel <= 0)
                    sb.Append($"데미지 {data.GetDamageForLevel(1)}");
                else
                    sb.Append($"데미지 {data.GetDamageForLevel(currentLevel)} → {data.GetDamageForLevel(currentLevel + 1)}");
            }

            // 쿨다운
            if (data.cooldownPerLevel != null && data.cooldownPerLevel.Length > 0)
            {
                if (sb.Length > 0) sb.Append(Separator);
                if (currentLevel <= 0)
                    sb.Append($"쿨다운 {data.GetCooldownForLevel(1):F2}초");
                else
                    sb.Append($"쿨다운 {data.GetCooldownForLevel(currentLevel):F2}초 → {data.GetCooldownForLevel(currentLevel + 1):F2}초");
            }

            return sb.ToString();
        }

        // ===== Passive =====

        private static string FormatPassive(SkillData data, int currentLevel)
        {
            if (data.bonusType == PassiveBonusType.None) return string.Empty;
            if (data.bonusPerLevel == 0f) return string.Empty;

            string label = GetPassiveLabel(data.bonusType);
            bool isPercent = IsPercentPassive(data.bonusType);

            // 패시브는 보유 시 currentLevel + 1 이 다음 레벨. bonus = bonusPerLevel × level.
            // 미보유(currentLevel=0) 는 첫 획득 시 효과 = bonusPerLevel × 1.
            if (currentLevel <= 0)
            {
                return $"{label} {FormatPassiveValue(data.bonusPerLevel, isPercent)}";
            }

            float curr = data.bonusPerLevel * currentLevel;
            float next = data.bonusPerLevel * (currentLevel + 1);
            return $"{label} {FormatPassiveValue(curr, isPercent)} → {FormatPassiveValue(next, isPercent)}";
        }

        private static string FormatPassiveValue(float value, bool isPercent)
        {
            if (isPercent)
            {
                // 0.2 → "+20%". 음수 가능성 대비 부호 분리.
                float pct = value * 100f;
                return $"{(pct >= 0 ? "+" : "")}{pct:0.##}%";
            }
            // 정수 가산 (ProjectileCount 등) 은 소수점 없게, 그 외 일반 가산.
            if (System.Math.Abs(value - System.Math.Round(value)) < 0.0001f)
                return $"{(value >= 0 ? "+" : "")}{(int)System.Math.Round(value)}";
            return $"{(value >= 0 ? "+" : "")}{value:0.##}";
        }

        private static string GetPassiveLabel(PassiveBonusType t)
        {
            switch (t)
            {
                case PassiveBonusType.ProjectileSpeed:    return "투사체 속도";
                case PassiveBonusType.ProjectileCount:    return "투사체 개수";
                case PassiveBonusType.SkillRange:         return "스킬 범위";
                case PassiveBonusType.SkillDuration:      return "스킬 지속시간";
                case PassiveBonusType.AttackMultiplier:   return "공격력";
                case PassiveBonusType.Knockback:          return "넉백";
                case PassiveBonusType.HealingMultiplier:  return "회복량";
                case PassiveBonusType.CritDamage:         return "치명타 데미지";
                case PassiveBonusType.CooldownReduction:  return "쿨다운 감소";
                case PassiveBonusType.MaxHP:              return "최대 체력";
                case PassiveBonusType.MoveSpeed:          return "이동 속도";
                case PassiveBonusType.Defense:            return "방어력";
                case PassiveBonusType.ExpMultiplier:      return "경험치";
                default:                                  return "보너스";
            }
        }

        /// <summary>비율 표기(%) 패시브 식별. 그 외는 "+N" 가산 표기.</summary>
        private static bool IsPercentPassive(PassiveBonusType t)
        {
            switch (t)
            {
                case PassiveBonusType.AttackMultiplier:
                case PassiveBonusType.CooldownReduction:
                case PassiveBonusType.Defense:
                case PassiveBonusType.HealingMultiplier:
                case PassiveBonusType.ExpMultiplier:
                case PassiveBonusType.CritDamage:
                    return true;
                default:
                    return false;
            }
        }

        // ===== Chaos =====

        private static string FormatChaos(SkillData data, Rarity rarity)
        {
            if (!(data is ChaosSkillData chaos)) return string.Empty;

            var p = chaos.GetParams(rarity);

            // 모든 값 0 = SO 미입력. 빈 문자열 반환 → SkillCardUI 에서 fallback description 사용.
            if (p.primary == 0f && p.secondary == 0f && p.tertiary == 0f)
                return string.Empty;

            switch (data.chaosEffectType)
            {
                case ChaosEffectType.GlassCannon:
                    // primary = ATK 배율, secondary = HP 비율
                    return $"공격력 ×{p.primary:0.##}\n최대 체력 ×{p.secondary:0.##}";

                case ChaosEffectType.ChainExplosion:
                    // primary = 폭발 데미지, secondary = 반경
                    return $"폭발 데미지 {(int)p.primary}\n반경 {p.secondary:0.##}m";

                case ChaosEffectType.BerserkMode:
                    // primary = CDR 배율, secondary = HP 임계, tertiary = 이속 배율
                    return $"HP {p.secondary * 100f:0.##}% 이하 시 발동\n쿨다운 ×{p.primary:0.##}\n이동속도 ×{p.tertiary:0.##}";

                case ChaosEffectType.AccelEngine:
                    // primary = 최대 증폭(%로 표기), secondary = 램프 초
                    return $"시간 경과 시 모든 스탯 +{p.primary * 100f:0.##}%\n(최대값까지 {p.secondary:0}초)";

                case ChaosEffectType.Unity:
                    // primary = 1명 근접 보너스(%), secondary = 인당 추가(%), tertiary = 반경
                    string radius = p.tertiary > 0f ? $"{p.tertiary:0.##}m" : "기본 반경";
                    return $"근접 아군 1명당 공격력 +{p.primary * 100f:0.##}%\n(추가 1명당 +{p.secondary * 100f:0.##}%, 감지 {radius})";

                case ChaosEffectType.Gambler:
                    return "레벨업 선택지 등급 상승\n(파티 전체 적용)";

                default:
                    return string.Empty;
            }
        }
    }
}
