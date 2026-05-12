using System;
using System.Collections.Generic;
using UnityEngine;
using SwDreams.Features.Unlock.Domain;

namespace SwDreams.Features.Unlock.Adapter.Data
{
    /// <summary>
    /// SO 가 없는 보상 전용 카탈로그 (meta-unlock.md §7).
    ///
    /// 본체 컨텐츠(스킬/무기/캐릭터)는 각자 SO 의 unlockConditions 에 분산형(D6)으로 부여.
    /// 본 카탈로그는 RefreshCharge 마일스톤(D7) + (미래) Cosmetic 슬롯만 다룬다.
    ///
    /// 셋업: Project 창에서 Create → SwDreams/Data/UnlockCatalog.
    /// 권장 기본값: 10/30/50회 클리어 시 +1 (총 +3).
    /// </summary>
    [CreateAssetMenu(fileName = "UnlockCatalog", menuName = "SwDreams/Data/UnlockCatalog")]
    public class UnlockCatalog : ScriptableObject
    {
        [Serializable]
        public struct RefreshChargeNode
        {
            [Tooltip("이 보너스를 활성화하는 조건. 일반적으로 RunsCleared(targetValue=N).")]
            public UnlockCondition condition;

            [Tooltip("충족 시 LevelUpManager 초기 충전에 가산되는 양. 보통 +1.")]
            public int amount;
        }

        [Header("새로고침 마일스톤 (D7)")]
        [Tooltip("권장: 10/30/50회 클리어 시 각 +1 (총 +3).\n" +
                 "각 노드의 condition 평가는 UnlockTracker 가 런 종료 시 일괄 수행.")]
        public List<RefreshChargeNode> refreshChargeNodes = new List<RefreshChargeNode>();

        [Header("(미래) 코스튬 — 슬롯만 예약")]
        [Tooltip("Cosmetic 시스템 자체는 본 plan 범위 밖. 슬롯 예약만.")]
        public List<UnlockCondition> cosmeticPlaceholders = new List<UnlockCondition>();
    }
}
