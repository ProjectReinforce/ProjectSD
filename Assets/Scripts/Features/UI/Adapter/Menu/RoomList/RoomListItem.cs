using Photon.Realtime;
using SwDreams.Features.Map.Adapter.Data;
using SwDreams.Features.UI.Adapter.Menu;
using SwDreams.Shared.Domain;
using SwDreams.Shared.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SwDreams.Features.UI.Adapter.Menu
{
    /// <summary>
    /// 방 리스트의 개별 아이템 UI.
    /// Slot_RoomList 프리팹에 부착한다.
    ///
    /// 프리팹 실제 계층 구조:
    ///   Slot_RoomList (Image 배경 + Button)          ← 이 스크립트 부착
    ///     ├─ Map (Image + Mask)
    ///     │    └─ Image_Map (Image)                  ← mapImage
    ///     ├─ Lock (빈 오브젝트)
    ///     │    ├─ Off (Image, 열린 자물쇠, 기본 활성) ← lockOffIcon
    ///     │    └─ On  (Image, 잠긴 자물쇠, 기본 비활성) ← lockOnIcon
    ///     ├─ Text_Name (TMP_Text, 왼쪽 정렬)         ← roomNameText
    ///     ├─ Text_Level (TMP_Text, "상")             ← difficultyText
    ///     └─ Text_Count (TMP_Text, "2/6")            ← playerCountText
    ///
    /// 책임(SRP):
    ///   - 자신에게 바인딩된 RoomInfo를 UI에 표시
    ///   - 클릭 시 IRoomListItemHandler에 이벤트 전달
    ///
    /// 설계 의도:
    ///   Button 컴포넌트는 프리팹 루트에 이미 붙어 있으므로,
    ///   Awake에서 GetComponent로 가져온다.
    ///   Lock 처리는 Off/On 두 Image를 교차 활성화하는 방식으로,
    ///   프리팹에 이미 구성된 스프라이트를 그대로 활용한다.
    /// </summary>
    public class RoomListItem : MonoBehaviour
    {
        [Header("Map")]
        [SerializeField] private Image mapImage;
        [Tooltip("방의 mapId 를 표시 sprite 로 매핑하기 위한 데이터베이스. 비워두면 미리보기 갱신 생략.")]
        [SerializeField] private MapDatabase mapDatabase;

        [Header("Lock (Off = 열림, On = 잠김)")]
        [SerializeField] private GameObject lockOffIcon;
        [SerializeField] private GameObject lockOnIcon;

        [Header("Text")]
        [SerializeField] private TMP_Text roomNameText;
        [SerializeField] private TMP_Text difficultyText;
        [SerializeField] private TMP_Text playerCountText;

        private Button itemButton;
        private RoomInfo boundRoom;
        private IRoomListItemHandler handler;

        private void Awake()
        {
            // 프리팹 루트에 Button이 이미 있으므로 GetComponent로 참조.
            // SerializeField로 따로 뚫지 않는 이유:
            //   Button은 이 스크립트와 같은 GameObject에 항상 존재하므로
            //   인스펙터 연결 실수를 줄이기 위해 자동 탐색한다.
            itemButton = GetComponent<Button>();
            if (itemButton != null)
            {
                itemButton.onClick.AddListener(HandleClick);
            }
        }

        private void OnDestroy()
        {
            if (itemButton != null)
            {
                itemButton.onClick.RemoveListener(HandleClick);
            }
        }

        /// <summary>
        /// RoomListView가 아이템을 활성화할 때 호출.
        /// 핸들러 참조와 방 정보를 함께 바인딩한다.
        /// </summary>
        public void Bind(RoomInfo room, IRoomListItemHandler itemHandler, bool isPasswordProtected)
        {
            boundRoom = room;
            handler = itemHandler;

            ApplyRoomData(room, isPasswordProtected);
            gameObject.SetActive(true);
        }

        /// <summary>
        /// 방 정보가 변경되었을 때(새로고침 시) UI만 갱신.
        /// 핸들러 참조는 유지하므로 불필요한 재할당이 없다.
        /// </summary>
        public void Refresh(RoomInfo room, bool isPasswordProtected)
        {
            boundRoom = room;
            ApplyRoomData(room, isPasswordProtected);
        }

        /// <summary>
        /// 비활성화하고 바인딩 해제.
        /// RoomListView가 풀로 반환할 때 호출.
        /// </summary>
        public void Unbind()
        {
            boundRoom = null;
            handler = null;
            gameObject.SetActive(false);
        }

        public RoomInfo BoundRoom => boundRoom;

        private void ApplyRoomData(RoomInfo room, bool isPasswordProtected)
        {
            // 방 이름
            if (roomNameText != null)
            {
                roomNameText.text = room.Name;
            }

            // 인원 수 (예: "2/6")
            if (playerCountText != null)
            {
                playerCountText.text = $"{room.PlayerCount}/{room.MaxPlayers}";
            }

            // 자물쇠: Off(열림)와 On(잠김)을 교차 활성화
            if (lockOffIcon != null)
            {
                lockOffIcon.SetActive(!isPasswordProtected);
            }

            if (lockOnIcon != null)
            {
                lockOnIcon.SetActive(isPasswordProtected);
            }

            // 난이도 — 방 CustomProperties[diff] 에서 읽어 한글 표기.
            if (difficultyText != null)
            {
                var diff = NetworkManager.GetRoomDifficulty(room);
                difficultyText.text = DifficultyDisplay(diff);
            }

            // 맵 미리보기 — mapDatabase 가 연결되어 있을 때만 갱신. 매칭 실패 시 프리팹 기본 sprite 유지.
            if (mapImage != null && mapDatabase != null)
            {
                var mapId = NetworkManager.GetRoomMapId(room);
                var map = mapDatabase.GetById(mapId) ?? mapDatabase.DefaultMap;
                if (map != null && map.PreviewSprite != null)
                {
                    mapImage.sprite = map.PreviewSprite;
                }
            }
        }

        private static string DifficultyDisplay(Difficulty d)
        {
            switch (d)
            {
                case Difficulty.Easy: return "쉬움";
                case Difficulty.Hard: return "어려움";
                default: return "보통";
            }
        }

        private void HandleClick()
        {
            if (boundRoom == null || handler == null)
            {
                return;
            }

            handler.OnRoomItemClicked(boundRoom);
        }
    }
}
