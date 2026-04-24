using UnityEngine;
using SwDreams.Features.Character.Domain.ValueObjects;
using SwDreams.Shared.Domain.ValueObjects;

namespace SwDreams.Features.StatBoost.Adapter.Data
{
    /// <summary>
    /// 능력치 부스트 SO (통합 등급 방식).
    /// 하나의 SO 가 (statType, op) 조합을 정의하고 등급별 value 테이블을 들고 있다.
    ///
    /// 선정 경로:
    /// 1. StatBoostChoiceService 가 Rarity 롤
    /// 2. Database.All 전체가 후보 → 롤된 등급에서의 value 를 꺼내 사용
    /// 3. 동일 boostId 라도 등급에 따라 다른 value 가 적용
    ///
    /// 장점 (이전 설계 대비):
    /// - SO 수 1/4 감소 — 같은 스탯의 4 등급 관리가 한 에셋에 집중
    /// - 밸런싱 한 화면에서 가능
    /// - 카드 3장 동일 등급 보장은 유지 (공통 RarityPoolChoiceGenerator 경로)
    ///
    /// 장착 시 PlayerStats.AddModifier(source="stat_{boostId}_{counter}").
    /// 동일 boostId 를 여러 번 획득해도 각 획득이 독립 modifier 로 누적.
    /// </summary>
    [CreateAssetMenu(fileName = "StatBoostData", menuName = "SwDreams/Data/StatBoostData")]
    public class StatBoostData : ScriptableObject
    {
        [Header("식별")]
        [Tooltip("고유 int ID. RPC 전송용. 다른 boostData 와 겹치지 않게.")]
        public int boostId;

        [Tooltip("HUD / 카드 표시용 한글 이름 (등급 무관).")]
        public string displayName;

        [Tooltip("카드 설명 텍스트 (등급 무관).")]
        [TextArea(1, 3)]
        public string description;

        [Header("시각")]
        public Sprite icon;

        [Header("효과 정의")]
        [Tooltip("영향 줄 스탯. 데미지 계열(AttackMultiplier) 은 op=PercentBonus 권장, " +
                 "나머지는 op=Add 권장.")]
        public StatType statType;

        public ModifierOp op = ModifierOp.Add;

        [Header("등급별 value (4원소: Common / Rare / Epic / Legendary)")]
        [Tooltip("선정 시점에 롤된 Rarity 로 인덱싱. 길이 4 고정.\n" +
                 "예) 공격력 +%: [0.05, 0.1, 0.2, 0.4] → +5% / +10% / +20% / +40%.")]
        public float[] valueByRarity = new float[4];

        /// <summary>
        /// 주어진 등급의 value 반환. 배열 범위 밖이면 0.
        /// </summary>
        public float GetValue(Rarity rarity)
        {
            int idx = (int)rarity;
            if (valueByRarity == null || idx < 0 || idx >= valueByRarity.Length) return 0f;
            return valueByRarity[idx];
        }

        private void OnValidate()
        {
            // valueByRarity 를 항상 길이 4 로 유지.
            if (valueByRarity == null || valueByRarity.Length != 4)
            {
                float[] resized = new float[4];
                if (valueByRarity != null)
                {
                    int n = Mathf.Min(valueByRarity.Length, 4);
                    for (int i = 0; i < n; i++) resized[i] = valueByRarity[i];
                }
                valueByRarity = resized;
            }
        }
    }
}
