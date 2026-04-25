using Photon.Pun;
using SwDreams.Features.Progression.Adapter;
using SwDreams.Features.Quest.Adapter.Data;
using UnityEngine;

namespace SwDreams.Features.Quest.Adapter
{
    /// <summary>
    /// 퀘스트 완료 시 보상(StatBoost 선택지) 트리거.
    /// LevelUpManager 의 StatBoost 경로를 재사용 (게임 일시정지 + 카드 + 타임아웃).
    ///
    /// 호스트 전용 호출. QuestZone.DispatchReward 에서만 사용.
    /// </summary>
    public static class QuestRewardDispatcher
    {
        public static void DispatchStatBoostReward(QuestData data)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (data == null) return;

            var manager = LevelUpManager.Instance;
            if (manager == null)
            {
                Debug.LogError("[QuestRewardDispatcher] LevelUpManager.Instance 없음 — 보상 dispatch 실패.");
                return;
            }

            Debug.Log($"[QuestRewardDispatcher] 퀘스트 [{data.displayName}] 보상 dispatch.");
            manager.RequestQuestReward(data.rewardRarityWeights);
        }
    }
}
