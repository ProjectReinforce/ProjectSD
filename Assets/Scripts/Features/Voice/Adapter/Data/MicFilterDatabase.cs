using System.Collections.Generic;
using UnityEngine;
using SwDreams.Features.Voice.Domain;

namespace SwDreams.Features.Voice.Adapter.Data
{
    /// <summary>
    /// 모든 MicFilterData 보관 SO. GameManager.MicFilterDB 로 SSOT 노출.
    ///
    /// DropSpawner: 픽업 결정 시 무작위 1종 인덱스 롤 → RaiseEvent 의 dataIdHash 자리에 인덱스 전송.
    /// MicFilterController: 인덱스 → MicFilterData 해결.
    /// 인덱스 = filters 리스트 순서. 같은 SO 에셋을 모든 클라가 참조하므로 안정.
    /// </summary>
    [CreateAssetMenu(fileName = "MicFilterDatabase", menuName = "ProjectSD/Data/MicFilterDatabase")]
    public class MicFilterDatabase : ScriptableObject
    {
        [SerializeField] private List<MicFilterData> filters = new();

        public IReadOnlyList<MicFilterData> All => filters;

        public int Count => filters?.Count ?? 0;

        public MicFilterData GetByIndex(int idx)
        {
            if (filters == null || idx < 0 || idx >= filters.Count) return null;
            return filters[idx];
        }

        public MicFilterData GetByType(MicFilterType type)
        {
            if (filters == null) return null;
            for (int i = 0; i < filters.Count; i++)
                if (filters[i] != null && filters[i].type == type) return filters[i];
            return null;
        }

        public int GetIndexOfType(MicFilterType type)
        {
            if (filters == null) return -1;
            for (int i = 0; i < filters.Count; i++)
                if (filters[i] != null && filters[i].type == type) return i;
            return -1;
        }
    }
}
