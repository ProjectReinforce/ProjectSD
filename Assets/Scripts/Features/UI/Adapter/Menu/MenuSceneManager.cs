using UnityEngine;
using Photon.Pun;
using SwDreams.Features.UI.Adapter.Menu;
using SwDreams.Shared.Managers;

namespace SwDreams.Features.UI.Adapter.Menu
{
    public class MenuSceneManager : MonoBehaviour
    {
        [SerializeField] private GameObject titlePanel;
        [SerializeField] private GameObject roomListPanel;
        [SerializeField] private GameObject waitingRoomPanel;

        [Tooltip("MenuScene에 상주하는 타이틀/룸리스트용 배경 이미지. 대기실에서만 숨긴다.")]
        [SerializeField] private GameObject titleBackground;

        /// <summary>
        /// 씬 전환 전에 이 플래그를 true로 설정하면,
        /// MenuScene 진입 시 타이틀 대신 방 리스트를 표시한다.
        /// 한 번 읽으면 소비(false로 리셋)된다.
        ///
        /// 왜 static인가:
        ///   씬 전환 시 모든 MonoBehaviour 인스턴스가 파괴되므로,
        ///   이전 씬에서 다음 씬으로 값을 전달하려면 static이어야 한다.
        ///   DontDestroyOnLoad 매니저에 넣는 방법도 있지만,
        ///   이 플래그는 MenuSceneManager만 읽고 쓰므로 여기에 두는 게 SRP에 맞다.
        /// </summary>
        public static bool ReturnToRoomList { get; set; }

        private void Start()
        {
            // Phase 7: 메뉴 BGM
            AudioManager.Instance?.PlayMenuBGM();

            // 방에 있으면 대기실 (다시 하기로 돌아온 경우)
            if (PhotonNetwork.InRoom)
            {
                ShowWaitingRoom();
                return;
            }

            // 게임씬 나가기로 돌아온 경우 → 방 리스트
            if (ReturnToRoomList)
            {
                ReturnToRoomList = false;
                ShowRoomList();
                return;
            }

            // 최초 진입 또는 일반적인 경우 → 타이틀
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

            // 대기실은 월드 공간에 캐릭터를 배치하므로 타이틀 배경을 숨긴다.
            // 타이틀/룸리스트로 돌아오면 다시 노출.
            if (titleBackground != null) titleBackground.SetActive(!waiting);
        }
    }
}
