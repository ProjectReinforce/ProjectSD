using System.Collections.Generic;
using SwDreams.Features.Character.Domain.ValueObjects;
using SwDreams.Features.Character.Adapter;

namespace SwDreams.Features.Character.Domain.ValueObjects
{
    /// <summary>
    /// StatModifier 컬렉션. 등록/제거/조회/계산을 담당.
    /// PlayerStats 내부에서 사용되며, MonoBehaviour에 의존하지 않는 순수 C# 클래스.
    /// 
    /// 스레드 안전성: 메인 스레드 단일 접근 전제 (Unity 특성).
    /// 
    /// [Phase 7 리팩토링] Step 1-1
    /// </summary>
    public class StatModifierCollection
    {
        private readonly List<StatModifier> modifiers = new List<StatModifier>();

        /// <summary>현재 등록된 modifier 수.</summary>
        public int Count => modifiers.Count;

        // ===== 등록 =====

        /// <summary>
        /// modifier 추가. 동일 source + statType 조합이 있으면 교체.
        /// </summary>
        /// <returns>true: 새로 추가됨, false: 기존 교체됨</returns>
        public bool AddOrReplace(StatModifier modifier)
        {
            for (int i = 0; i < modifiers.Count; i++)
            {
                if (modifiers[i].Source == modifier.Source &&
                    modifiers[i].StatType == modifier.StatType)
                {
                    modifiers[i] = modifier;
                    return false;
                }
            }

            modifiers.Add(modifier);
            return true;
        }

        /// <summary>
        /// source가 일치하는 모든 modifier 제거.
        /// 패시브 스킬 제거 시 해당 패시브의 모든 보너스를 한 번에 제거.
        /// </summary>
        /// <returns>제거된 modifier 수</returns>
        public int RemoveBySource(string source)
        {
            int removed = 0;
            for (int i = modifiers.Count - 1; i >= 0; i--)
            {
                if (modifiers[i].Source == source)
                {
                    modifiers.RemoveAt(i);
                    removed++;
                }
            }
            return removed;
        }

        /// <summary>
        /// source 접두사가 일치하는 모든 modifier 제거.
        /// 예: RemoveBySourcePrefix("passive_") → 모든 패시브 modifier 제거.
        /// </summary>
        /// <returns>제거된 modifier 수</returns>
        public int RemoveBySourcePrefix(string prefix)
        {
            int removed = 0;
            for (int i = modifiers.Count - 1; i >= 0; i--)
            {
                if (modifiers[i].Source.StartsWith(prefix))
                {
                    modifiers.RemoveAt(i);
                    removed++;
                }
            }
            return removed;
        }

        /// <summary>
        /// source의 접두사를 변경. 진화 시 패시브 → 진화 승계 용도.
        /// 예: ReplaceSourcePrefix("passive_투사체속도", "evolution_폭렬표창")
        /// </summary>
        /// <returns>변경된 modifier 수</returns>
        public int ReplaceSource(string oldSource, string newSource)
        {
            int changed = 0;
            for (int i = 0; i < modifiers.Count; i++)
            {
                if (modifiers[i].Source == oldSource)
                {
                    var m = modifiers[i];
                    modifiers[i] = new StatModifier(newSource, m.StatType, m.Operation, m.Value);
                    changed++;
                }
            }
            return changed;
        }

        /// <summary>모든 modifier 제거.</summary>
        public void Clear()
        {
            modifiers.Clear();
        }

        // ===== 계산 =====

        /// <summary>
        /// 특정 StatType의 최종값 계산.
        /// 공식: (baseValue + ΣAdd) × (1 + ΣPercentBonus) × ΠMultiplicative
        ///
        /// - Add: 플랫 가산.
        /// - PercentBonus: 가산 % 스택 (0=기본). 예) 0.1 + 0.1 = 0.2 → ×1.2.
        /// - Multiplicative: 배율 곱 스택 (1=기본). 예) 0.5 × 2.0 = ×1.0.
        /// </summary>
        public float Calculate(StatType type, float baseValue)
        {
            float addSum = 0f;
            float percentBonusSum = 0f;
            float mulProduct = 1f;

            for (int i = 0; i < modifiers.Count; i++)
            {
                if (modifiers[i].StatType != type) continue;

                switch (modifiers[i].Operation)
                {
                    case ModifierOp.Add:
                        addSum += modifiers[i].Value;
                        break;
                    case ModifierOp.PercentBonus:
                        percentBonusSum += modifiers[i].Value;
                        break;
                    case ModifierOp.Multiplicative:
                        mulProduct *= modifiers[i].Value;
                        break;
                }
            }

            return (baseValue + addSum) * (1f + percentBonusSum) * mulProduct;
        }

        /// <summary>
        /// 특정 StatType의 Add 합계만 반환. 
        /// base 없이 보너스만 필요할 때 (예: ProjectileCountBonus).
        /// </summary>
        public float GetAddTotal(StatType type)
        {
            float sum = 0f;
            for (int i = 0; i < modifiers.Count; i++)
            {
                if (modifiers[i].StatType == type &&
                    modifiers[i].Operation == ModifierOp.Add)
                    sum += modifiers[i].Value;
            }
            return sum;
        }

        /// <summary>
        /// 특정 StatType의 Multiplicative 누적값 (ΠMultiplicative) 반환.
        /// GetEffectiveCooldown 처럼 공식 외부에서 CDR(Add) 와 쿨다운 배율을 분리 적용할 때 사용.
        /// </summary>
        public float GetMultiplicativeTotal(StatType type)
        {
            float product = 1f;
            for (int i = 0; i < modifiers.Count; i++)
            {
                if (modifiers[i].StatType == type &&
                    modifiers[i].Operation == ModifierOp.Multiplicative)
                    product *= modifiers[i].Value;
            }
            return product;
        }

        /// <summary>
        /// 특정 StatType의 PercentBonus 가산 합 반환. 디버그용.
        /// 공식 최종 인수는 (1 + 이 값).
        /// </summary>
        public float GetPercentBonusTotal(StatType type)
        {
            float sum = 0f;
            for (int i = 0; i < modifiers.Count; i++)
            {
                if (modifiers[i].StatType == type &&
                    modifiers[i].Operation == ModifierOp.PercentBonus)
                    sum += modifiers[i].Value;
            }
            return sum;
        }

        // ===== 조회 =====

        /// <summary>특정 source의 modifier가 존재하는지.</summary>
        public bool HasSource(string source)
        {
            for (int i = 0; i < modifiers.Count; i++)
            {
                if (modifiers[i].Source == source)
                    return true;
            }
            return false;
        }

        /// <summary>읽기 전용 전체 목록. 디버그/UI용.</summary>
        public IReadOnlyList<StatModifier> GetAll()
        {
            return modifiers;
        }

        /// <summary>디버그 문자열. 모든 modifier를 줄 단위로 출력.</summary>
        public string ToDebugString()
        {
            if (modifiers.Count == 0) return "(empty)";

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < modifiers.Count; i++)
            {
                if (i > 0) sb.AppendLine();
                sb.Append(modifiers[i].ToString());
            }
            return sb.ToString();
        }
    }
}