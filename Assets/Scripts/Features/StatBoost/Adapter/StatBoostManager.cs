using System.Collections.Generic;
using UnityEngine;
using SwDreams.Features.Character.Domain.ValueObjects;
using SwDreams.Features.StatBoost.Adapter.Data;
using SwDreams.Shared.Domain.Interfaces;

namespace SwDreams.Features.StatBoost.Adapter
{
    /// <summary>
    /// 플레이어의 능력치 부스트 기록. Player 프리팹 자식에 부착.
    ///
    /// 동일 boostId 를 여러 번 획득해도 각 획득이 독립 modifier 로 등록돼 누적된다.
    /// source 네이밍: "stat_{boostId}_{localCounter}" — counter 는 본 매니저의 로컬 시퀀스.
    ///
    /// 네트워크: 적용은 로컬 전용 (LevelUpManager 가 각 클라에 sync 해주는 기존 패턴 따름).
    /// 중도 참가자 동기화는 LevelUpManager 경로에 의존 — 별도 AllBuffered 안 씀.
    /// </summary>
    public class StatBoostManager : MonoBehaviour
    {
        private const string SourcePrefix = "stat_";

        private IPlayerStatsMutator stats;
        private int counter;

        // 디버그/HUD 표시용 — 획득 순서대로 기록.
        private readonly List<StatBoostData> applied = new List<StatBoostData>();
        public IReadOnlyList<StatBoostData> Applied => applied;
        public int AppliedCount => applied.Count;

        private void Awake()
        {
            EnsureStatsRef();
        }

        /// <summary>
        /// 선택된 StatBoost 를 적용. 로컬 호출 경로 전용
        /// (LevelUpManager.SubmitChoice → ApplyChoice + 원격 sync RPC 로 위임).
        /// </summary>
        public void ApplyChoice(StatBoostData data)
        {
            if (data == null) return;
            EnsureStatsRef();
            if (stats == null)
            {
                Debug.LogWarning("[StatBoostManager] IPlayerStatsMutator 포트 미연결 — 적용 실패.");
                return;
            }

            counter++;
            string source = $"{SourcePrefix}{data.boostId}_{counter}";
            stats.AddModifier(new StatModifier(source, data.statType, data.op, data.value));
            stats.Recalculate();

            applied.Add(data);
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
