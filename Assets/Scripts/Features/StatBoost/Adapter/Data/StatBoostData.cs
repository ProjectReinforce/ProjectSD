using UnityEngine;
using SwDreams.Features.Character.Domain.ValueObjects;
using SwDreams.Shared.Domain.ValueObjects;

namespace SwDreams.Features.StatBoost.Adapter.Data
{
    /// <summary>
    /// 능력치 부스트 SO. 만렙(스킬 풀 고갈) 레벨업 선택지 + 퀘스트 보상 공용.
    ///
    /// 장착 시 PlayerStats.AddModifier(source="stat_{boostId}_{counter}") 로 등록.
    /// - counter 는 StatBoostManager 가 apply 시마다 증가 → 중복 선택 누적 가능.
    /// - 3-op 의미 구분 (AttackMultiplier 는 PercentBonus, 다른 스탯은 Add 권장).
    /// </summary>
    [CreateAssetMenu(fileName = "StatBoostData", menuName = "SwDreams/Data/StatBoostData")]
    public class StatBoostData : ScriptableObject
    {
        [Header("식별")]
        [Tooltip("고유 int ID. RPC 전송용. 다른 boostData 와 겹치지 않게.")]
        public int boostId;

        [Tooltip("HUD / 카드 표시용 한글 이름.")]
        public string displayName;

        [Tooltip("카드 설명 텍스트.")]
        [TextArea(1, 3)]
        public string description;

        [Header("시각")]
        public Sprite icon;

        [Header("등급")]
        public Rarity rarity = Rarity.Common;

        [Header("효과")]
        [Tooltip("어떤 스탯을 어떤 연산으로 얼마나 증가시킬지.\n" +
                 "데미지 계열(AttackMultiplier) 은 PercentBonus 권장, 나머지는 Add 권장.")]
        public StatType statType;

        public ModifierOp op = ModifierOp.Add;

        public float value;
    }
}
