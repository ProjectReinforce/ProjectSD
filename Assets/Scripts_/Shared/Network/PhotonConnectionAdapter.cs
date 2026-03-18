using Photon.Pun;
using UnityEngine;

namespace Shared.Network
{
    /// <summary>
    /// Photon 서버 연결 및 로비 진입을 담당하는 어댑터.
    /// 씬에 배치하면 Start에서 자동 연결한다.
    /// </summary>
    public sealed class PhotonConnectionAdapter : MonoBehaviourPunCallbacks
    {
        [SerializeField]
        private readonly bool connectOnStart = true;

        private void Start()
        {
            if (connectOnStart)
                Connect();
        }

        public void Connect()
        {
            if (PhotonNetwork.IsConnected)
            {
                Debug.Log("[PhotonConnection] Already connected.");
                return;
            }

            PhotonNetwork.AutomaticallySyncScene = true;
            PhotonNetwork.ConnectUsingSettings();
            Debug.Log("[PhotonConnection] Connecting...");
        }

        public override void OnConnectedToMaster()
        {
            Debug.Log("[PhotonConnection] Connected to Master. Joining lobby...");
            PhotonNetwork.JoinLobby();
        }

        public override void OnJoinedLobby()
        {
            Debug.Log("[PhotonConnection] Joined lobby. Ready for matchmaking.");
        }
    }
}
