using UnityEngine;
using Photon.Pun;
using ExitGames.Client.Photon;

namespace Adapter.UI.Menu
{
    public class MenuSceneManager : MonoBehaviour
    {
        private const string ReturnToWaitingRoomKey = "returnToWaitingRoom";

        [SerializeField] private GameObject titlePanel;
        [SerializeField] private GameObject roomListPanel;
        [SerializeField] private GameObject waitingRoomPanel;

        private void Start()
        {
            // "다시 하기"로 돌아온 경우: 대기실 직행
            if (PhotonNetwork.InRoom && CheckAndClearReturnFlag())
            {
                ShowWaitingRoom();
                return;
            }

            ShowTitle();
        }

        /// <summary>
        /// Room CustomProperties에서 returnToWaitingRoom 플래그 확인 후 제거.
        /// ResultManager.ExecuteRetry()에서 설정한 플래그.
        /// </summary>
        private bool CheckAndClearReturnFlag()
        {
            if (PhotonNetwork.CurrentRoom == null) return false;

            var props = PhotonNetwork.CurrentRoom.CustomProperties;
            if (!props.TryGetValue(ReturnToWaitingRoomKey, out var value)) return false;
            if (value is not bool flag || !flag) return false;

            // 플래그 제거 (호스트만)
            if (PhotonNetwork.IsMasterClient)
            {
                var clearProps = new Hashtable { [ReturnToWaitingRoomKey] = null };
                PhotonNetwork.CurrentRoom.SetCustomProperties(clearProps);
            }

            Debug.Log("[MenuSceneManager] returnToWaitingRoom 플래그 감지 → 대기실 직행");
            return true;
        }

        public void ShowTitle()
        {
            SetPanels(true, false, false);
        }

        public void ShowRoomList()
        {
            SetPanels(false, true, false);
        }

        public void ShowWaitingRoom()
        {
            SetPanels(false, false, true);
        }

        private void SetPanels(bool title, bool roomList, bool waiting)
        {
            if (titlePanel != null) titlePanel.SetActive(title);
            if (roomListPanel != null) roomListPanel.SetActive(roomList);
            if (waitingRoomPanel != null) waitingRoomPanel.SetActive(waiting);
        }
    }
}
