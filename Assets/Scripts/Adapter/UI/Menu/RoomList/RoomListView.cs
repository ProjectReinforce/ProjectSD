using System.Collections.Generic;
using Photon.Realtime;
using UnityEngine;

namespace Adapter.UI.Menu
{
    /// <summary>
    /// 방 리스트 아이템의 생성, 재활용, 정리를 담당하는 View 클래스.
    ///
    /// 설계 의도(SRP):
    ///   RoomListPanelController가 "방 목록이 바뀌었다"고 알려주면,
    ///   이 클래스가 실제 GameObject의 생성/재활용/비활성화를 처리한다.
    ///   Controller는 UI 오브젝트 관리를 모르고, View는 비즈니스 로직을 모른다.
    ///
    /// 풀링 전략:
    ///   - 방이 새로 추가되면 풀에서 비활성 아이템을 꺼내거나 새로 Instantiate
    ///   - 방이 사라지면 아이템을 Unbind하고 풀에 반환 (Destroy 안 함)
    ///   - 기존 방은 Refresh()로 텍스트만 갱신 (GameObject 재생성 없음)
    ///
    /// Unity 셋업:
    ///   - roomItemPrefab: RoomListItem 컴포넌트가 붙은 프리팹
    ///   - contentParent: ScrollView의 Content (VerticalLayoutGroup 권장)
    /// </summary>
    public class RoomListView : MonoBehaviour
    {
        [SerializeField] private RoomListItem roomItemPrefab;
        [SerializeField] private Transform contentParent;

        // roomName → 활성 아이템 매핑. 빠른 조회를 위해 Dictionary 사용.
        private readonly Dictionary<string, RoomListItem> activeItems = new Dictionary<string, RoomListItem>();

        // 비활성 상태의 재활용 가능한 아이템 풀.
        private readonly List<RoomListItem> pool = new List<RoomListItem>();

        /// <summary>
        /// 현재 활성화된 아이템 수. 외부에서 "No rooms available" 표시 판단용.
        /// </summary>
        public int ActiveItemCount => activeItems.Count;

        /// <summary>
        /// 방 목록이 갱신될 때 Controller가 호출.
        /// rooms 배열과 현재 활성 아이템을 비교해서 최소한의 변경만 수행한다.
        ///
        /// 왜 전체 삭제 후 재생성이 아닌가:
        ///   매 새로고침마다 모든 아이템을 Destroy/Instantiate하면
        ///   GC 압력과 UI 깜빡임이 발생한다.
        ///   대신 diff 방식으로 추가/제거/갱신을 구분한다.
        /// </summary>
        public void SyncItems(
            RoomInfo[] rooms,
            IRoomListItemHandler handler,
            System.Func<RoomInfo, bool> isPasswordProtected,
            System.Func<RoomInfo, bool> filter)
        {
            // 1단계: 현재 서버에 존재하는 방 이름 수집
            var serverRoomNames = new HashSet<string>();
            for (var i = 0; i < rooms.Length; i++)
            {
                var room = rooms[i];
                if (filter != null && !filter(room))
                {
                    continue;
                }

                serverRoomNames.Add(room.Name);
            }

            // 2단계: 서버에 없는 방의 아이템 제거 (풀로 반환)
            var toRemove = new List<string>();
            foreach (var kvp in activeItems)
            {
                if (!serverRoomNames.Contains(kvp.Key))
                {
                    toRemove.Add(kvp.Key);
                }
            }

            for (var i = 0; i < toRemove.Count; i++)
            {
                ReturnToPool(toRemove[i]);
            }

            // 3단계: 방 목록 순회 — 기존 아이템은 Refresh, 새 방은 Bind
            for (var i = 0; i < rooms.Length; i++)
            {
                var room = rooms[i];
                if (filter != null && !filter(room))
                {
                    continue;
                }

                var hasPw = isPasswordProtected != null && isPasswordProtected(room);

                if (activeItems.TryGetValue(room.Name, out var existingItem))
                {
                    // 이미 표시 중인 방 → UI 텍스트만 갱신
                    existingItem.Refresh(room, hasPw);
                    // 순서 보장: sibling index를 현재 순번으로 설정
                    existingItem.transform.SetSiblingIndex(i);
                }
                else
                {
                    // 새로 추가된 방 → 풀에서 가져오거나 새로 생성
                    var item = GetOrCreateItem();
                    item.Bind(room, handler, hasPw);
                    item.transform.SetSiblingIndex(i);
                    activeItems[room.Name] = item;
                }
            }
        }

        /// <summary>
        /// 모든 아이템을 비활성화하고 풀로 반환.
        /// 패널이 닫히거나 로비를 떠날 때 호출.
        /// </summary>
        public void ClearAll()
        {
            foreach (var kvp in activeItems)
            {
                var item = kvp.Value;
                item.Unbind();
                pool.Add(item);
            }

            activeItems.Clear();
        }

        private RoomListItem GetOrCreateItem()
        {
            // 풀에 재활용 가능한 아이템이 있으면 사용
            for (var i = pool.Count - 1; i >= 0; i--)
            {
                var pooled = pool[i];
                if (pooled != null)
                {
                    pool.RemoveAt(i);
                    return pooled;
                }

                // null 참조 정리 (씬 전환 등으로 파괴된 경우)
                pool.RemoveAt(i);
            }

            // 풀이 비었으면 새로 생성
            if (roomItemPrefab == null || contentParent == null)
            {
                Debug.LogError("[RoomListView] roomItemPrefab 또는 contentParent가 할당되지 않았습니다.");
                return null;
            }

            var newItem = Instantiate(roomItemPrefab, contentParent);
            newItem.gameObject.SetActive(false);
            return newItem;
        }

        private void ReturnToPool(string roomName)
        {
            if (!activeItems.TryGetValue(roomName, out var item))
            {
                return;
            }

            activeItems.Remove(roomName);
            item.Unbind();
            pool.Add(item);
        }
    }
}
