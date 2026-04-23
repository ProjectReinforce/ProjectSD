using System;
using SwDreams.Features.Character.Domain.ValueObjects;

namespace SwDreams.Features.Weapon.Domain
{
    /// <summary>
    /// 무기 SO 에서 인스펙터 편집 가능한 스탯 보정 엔트리.
    ///
    /// StatModifier 는 readonly struct 라 Inspector 직렬화 불가.
    /// WeaponData 는 이 Serializable 구조를 배열로 들고 있다가 장착 시점에
    /// source="weapon_{id}" 조합으로 StatModifier 로 변환해 PlayerStats.AddModifier 에 전달.
    ///
    /// Clean Architecture: Domain 레이어 — UnityEngine 의존 금지, SerializeField 는
    /// [System.Serializable] + public 필드로 대체.
    /// </summary>
    [Serializable]
    public struct WeaponStatEntry
    {
        public StatType statType;
        public ModifierOp op;
        public float value;

        /// <summary>
        /// true 면 같은 무기를 여러 개 장착해도 이 엔트리는 1회분만 적용된다.
        /// (PlayerStats source 네이밍이 슬롯 인덱스를 생략 → AddOrReplace 규칙으로 교체.)
        /// false 면 각 슬롯이 독립적으로 누적.
        /// </summary>
        public bool isUnique;

        public WeaponStatEntry(StatType statType, ModifierOp op, float value, bool isUnique = false)
        {
            this.statType = statType;
            this.op = op;
            this.value = value;
            this.isUnique = isUnique;
        }

        public override string ToString()
        {
            string uniq = isUnique ? " [U]" : string.Empty;
            switch (op)
            {
                case ModifierOp.Add:
                    return $"{statType} +{value}{uniq}";
                case ModifierOp.PercentBonus:
                    // value 0.1 = +10% 의미. 부호는 값에서 파생.
                    return $"{statType} {(value >= 0 ? "+" : "")}{value * 100f:0.##}%{uniq}";
                case ModifierOp.Multiplicative:
                    return $"{statType} ×{value}{uniq}";
                default:
                    return $"{statType} ?{value}{uniq}";
            }
        }
    }
}
