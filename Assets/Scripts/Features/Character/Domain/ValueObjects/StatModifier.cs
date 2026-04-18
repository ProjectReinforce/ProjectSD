namespace SwDreams.Features.Character.Domain.ValueObjects
{
    /// <summary>
    /// 스탯 수정자 하나의 단위. 불변(immutable) 구조체.
    /// 
    /// source 명명 규칙:
    ///   "passive_{skillName}_Lv{n}"  — 패시브 스킬
    ///   "chaos_{effectName}"         — 혼돈 스킬
    ///   "evolution_{skillName}"      — 진화 시 승계된 패시브
    ///   "character_base"             — 캐릭터 고유 보정 (추후)
    ///   "buff_{buffName}"            — 일시적 버프 (추후)
    /// 
    /// [Phase 7 리팩토링] Step 1-1
    /// </summary>
    public readonly struct StatModifier
    {
        /// <summary>수정자의 출처 식별자. 같은 source의 modifier는 교체(Replace)됨.</summary>
        public readonly string Source;

        /// <summary>영향을 주는 스탯 종류.</summary>
        public readonly StatType StatType;

        /// <summary>연산 타입 (Add / Multiply).</summary>
        public readonly ModifierOp Operation;

        /// <summary>수치. Add면 가산값, Multiply면 곱연산 배율 (1.0 = 변동 없음).</summary>
        public readonly float Value;

        public StatModifier(string source, StatType statType, ModifierOp operation, float value)
        {
            Source = source;
            StatType = statType;
            Operation = operation;
            Value = value;
        }

        public override string ToString()
        {
            string op = Operation == ModifierOp.Add ? "+" : "×";
            return $"[{Source}] {StatType} {op}{Value}";
        }
    }
}