using UnityEngine;
using Photon.Pun;
using SwDreams.Features.UI.Adapter.Menu;
using SwDreams.Features.UI.Presentation;
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

            // 룸 입장/생성 실패 안내는 메뉴씬 어느 패널에서 트리거되었든 보여야 하므로,
            // 자식 패널 컨트롤러가 아닌 메뉴씬 매니저 (씬 활성 기간 내내 살아있음) 에서 일원 구독.
            // 비밀번호 검증 실패(NetworkManager.OnJoinedRoom 내부에서 발화) 같은 분기에서도 누락 없음.
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.JoinRoomFailed += HandleJoinRoomFailedGlobal;
                NetworkManager.Instance.CreateRoomFailed += HandleCreateRoomFailedGlobal;
            }

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

        private void OnDestroy()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.JoinRoomFailed -= HandleJoinRoomFailedGlobal;
                NetworkManager.Instance.CreateRoomFailed -= HandleCreateRoomFailedGlobal;
            }
        }

        private static void HandleJoinRoomFailedGlobal(short returnCode, string message)
        {
            // 비밀번호 검증 분기(NetworkManager 내부 -1001) 와 일반 입장 실패를 동일하게 안내.
            // 상세 returnCode/message 는 NetworkManager 측 Debug.LogWarning 에 남는다.
            FrameToastController.Show(returnCode == -1001
                ? "비밀번호가 일치하지 않습니다"
                : "방 입장에 실패했습니다");
        }

        private static void HandleCreateRoomFailedGlobal(short returnCode, string message)
        {
            FrameToastController.Show("방 생성에 실패했습니다");
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
