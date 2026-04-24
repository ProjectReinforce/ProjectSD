using System.Collections.Generic;
using UnityEngine;
using SwDreams.Shared.Domain.ValueObjects;

namespace SwDreams.Features.StatBoost.Adapter.Data
{
    /// <summary>
    /// 모든 StatBoostData 를 보관하는 SO 루트. GameManager.StatBoostDB 로 SSOT 노출.
    ///
    /// StatBoostChoiceService 가 등급 풀 선정 시 <see cref="All"/> 을 넘겨 사용.
    /// RPC 경로는 <see cref="GetById"/> 로 int → SO 해결.
    /// </summary>
    [CreateAssetMenu(fileName = "StatBoostDatabase", menuName = "SwDreams/Data/StatBoostDatabase")]
    public class StatBoostDatabase : ScriptableObject
    {
        [SerializeField] private List<StatBoostData> boosts = new List<StatBoostData>();

        private Dictionary<int, StatBoostData> idLookup;

        public IReadOnlyList<StatBoostData> All => boosts;

        public StatBoostData GetById(int boostId)
        {
            EnsureLookup();
            return idLookup.TryGetValue(boostId, out var d) ? d : null;
        }

        private void EnsureLookup()
        {
            if (idLookup != null && idLookup.Count == CountNonNull()) return;
            idLookup = new Dictionary<int, StatBoostData>(boosts.Count);
            for (int i = 0; i < boosts.Count; i++)
            {
                var b = boosts[i];
                if (b == null) continue;
                idLookup[b.boostId] = b;
            }
        }

        private int CountNonNull()
        {
            int n = 0;
            for (int i = 0; i < boosts.Count; i++) if (boosts[i] != null) n++;
            return n;
        }

        private void OnValidate()
        {
            idLookup = null;
        }
    }
}
