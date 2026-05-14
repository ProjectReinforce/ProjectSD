using UnityEngine;

namespace SwDreams.Features.Map.Adapter.Data
{
    /// <summary>
    /// 사용 가능한 맵 목록. SceneTransitionManager / RoomListPanelController 가 인스펙터로 참조.
    /// id 충돌은 에디터 검증으로 거른다 (런타임 GetById 는 선형 탐색이라 N 수십 개까지 무리 없음).
    /// </summary>
    [CreateAssetMenu(fileName = "MapDatabase", menuName = "SwDreams/Map/MapDatabase")]
    public class MapDatabase : ScriptableObject
    {
        [Tooltip("등록된 맵 정의 목록. 첫 번째 요소가 기본 선택값.")]
        [SerializeField] private MapDefinition[] maps;

        public MapDefinition[] Maps => maps;
        public int Count => maps == null ? 0 : maps.Length;

        /// <summary>첫 번째 맵을 기본값으로 반환. 목록이 비어있으면 null.</summary>
        public MapDefinition DefaultMap => (maps != null && maps.Length > 0) ? maps[0] : null;

        /// <summary>id 일치 맵 반환. 없으면 null.</summary>
        public MapDefinition GetById(string id)
        {
            if (maps == null || string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < maps.Length; i++)
            {
                if (maps[i] != null && maps[i].Id == id) return maps[i];
            }
            return null;
        }
    }
}
