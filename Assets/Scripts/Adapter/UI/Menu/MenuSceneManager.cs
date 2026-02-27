using UnityEngine;

namespace Adapter.UI.Menu
{
    public class MenuSceneManager : MonoBehaviour
    {
        [SerializeField] private GameObject titlePanel;
        [SerializeField] private GameObject roomListPanel;
        [SerializeField] private GameObject waitingRoomPanel;

        private void Start()
        {
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
