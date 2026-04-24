using System.Collections.Generic;
using UnityEngine;
using SwDreams.Features.Character.Domain.ValueObjects;
using SwDreams.Features.StatBoost.Adapter.Data;
using SwDreams.Shared.Domain.Interfaces;
using SwDreams.Shared.Domain.ValueObjects;

namespace SwDreams.Features.StatBoost.Adapter
{
    /// <summary>
    /// 플레이어의 능력치 부스트 기록. Player 프리팹 자식에 부착.
    ///
    /// ApplyChoice(data, rarity) 로 적용 — SO 는 등급 무관, rarity 가 value 선택자.
    /// 동일 boostId 를 여러 번 획득해도 각 획득이 독립 modifier 로 등록돼 누적된다.
    /// source 네이밍: "stat_{boostId}_{rarity}_{localCounter}" — counter 는 로컬 시퀀스.
    ///
    /// 네트워크: 적용은 로컬 전용 (LevelUpManager 가 각 클라에 sync 해주는 기존 패턴).
    /// </summary>
    public class StatBoostManager : MonoBehaviour
    {
        private const string SourcePrefix = "stat_";

        private IPlayerStatsMutator stats;
        private int counter;

        // 디버그/HUD 표시용 — 획득 이력 (data + 해석된 rarity + value) 기록.
        public struct AppliedEntry
        {
            public StatBoostData data;
            public Rarity rarity;
            public float appliedValue;
        }

        private readonly List<AppliedEntry> applied = new List<AppliedEntry>();
        public IReadOnlyList<AppliedEntry> Applied => applied;
        public int AppliedCount => applied.Count;

        private void Awake()
        {
            EnsureStatsRef();
        }

        /// <summary>
        /// 선택된 StatBoost 를 등급과 함께 적용. LevelUpManager.SubmitChoice 경로 전용.
        /// </summary>
        public void ApplyChoice(StatBoostData data, Rarity rarity)
        {
            if (data == null) return;
            EnsureStatsRef();
            if (stats == null)
            {
                Debug.LogWarning("[StatBoostManager] IPlayerStatsMutator 포트 미연결 — 적용 실패.");
                return;
            }

            float value = data.GetValue(rarity);
            if (value == 0f)
            {
                Debug.LogWarning($"[StatBoostManager] {data.displayName} 의 {rarity} 등급 value 가 0 — SO 점검.");
            }

            counter++;
            string source = $"{SourcePrefix}{data.boostId}_{rarity.ToString().ToLower()}_{counter}";
            stats.AddModifier(new StatModifier(source, data.statType, data.op, value));
            stats.Recalculate();

            applied.Add(new AppliedEntry { data = data, rarity = rarity, appliedValue = value });
        }

        private void EnsureStatsRef()
        {
            if (stats != null) return;

            Transform cur = transform;
            Transform playerRoot = null;
            while (cur != null)
            {
                if (cur.CompareTag("Player")) { playerRoot = cur; break; }
                cur = cur.parent;
            }

            if (playerRoot != null)
                stats = playerRoot.GetComponentInChildren<IPlayerStatsMutator>();

            if (stats == null) stats = GetComponentInParent<IPlayerStatsMutator>();
            if (stats == null) stats = GetComponentInChildren<IPlayerStatsMutator>();
        }
    }
}
