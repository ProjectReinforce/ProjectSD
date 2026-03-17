using UnityEngine;
using Photon.Pun;

namespace Adapter.UI.Menu
{
    public class MenuSceneManager : MonoBehaviour
    {
        [SerializeField] private GameObject titlePanel;
        [SerializeField] private GameObject roomListPanel;
        [SerializeField] private GameObject waitingRoomPanel;

        private void Start()
        {
            // 방에 있으면 대기실 (다시 하기로 돌아온 경우)
            // 방에 없으면 타이틀 (나가기로 돌아왔거나 최초 진입)
            if (PhotonNetwork.InRoom)
            {
                ShowWaitingRoom();
                return;
            }

            ShowTitle();
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
