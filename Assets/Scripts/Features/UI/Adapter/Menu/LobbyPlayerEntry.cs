using Photon.Pun;
using Photon.Realtime;
using SwDreams.Shared.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SwDreams.Features.UI.Adapter.Menu
{
    /// <summary>
    /// 대기실 플레이어 리스트 한 줄(행) 컴포넌트.
    ///
    /// 무엇: 한 플레이어의 이름/역할(Host-Client)/캐릭터 ID/준비 상태를 보여주고,
    ///      호스트 시점에서만 활성화되는 Kick 버튼을 제공.
    /// 왜:   기존 playersStatusText 한 덩어리 TMP로는 각 플레이어별로 인터랙션을 달 수 없음.
    ///      강퇴 UI를 얹기 위해 행 단위 프리팹으로 분리.
    /// 어떻게: WaitingRoomPanelController가 PlayerList를 순회하며 이 프리팹을 재사용 풀로
    ///        Bind(Player)하고, 로컬이 마스터이면 KickButton을 활성화한다.
    ///        Kick 버튼은 NetworkManager.KickPlayer로 위임.
    ///
    /// R14 보이스 슬라이더는 본 행이 아니라 대기실 좌측 별도 VoicePanel 에서 처리 (인게임과 통일).
    /// </summary>
    public class LobbyPlayerEntry : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text roleText;
        [SerializeField] private TMP_Text characterText;
        [SerializeField] private TMP_Text readyText;
        [SerializeField] private Button kickButton;

        private Player boundPlayer;

        private void Awake()
        {
            if (kickButton != null)
            {
                kickButton.onClick.AddListener(OnClickKick);
            }
        }

        private void OnDestroy()
        {
            if (kickButton != null)
            {
                kickButton.onClick.RemoveListener(OnClickKick);
            }
        }

        /// <summary>
        /// 행을 특정 플레이어에 바인딩. 매 갱신마다 호출해도 안전.
        /// </summary>
        public void Bind(Player player)
        {
            boundPlayer = player;
            if (player == null)
            {
                Clear();
                return;
            }

            // 방 퇴장 직후 재호출될 때 PhotonNetwork.LocalPlayer 접근은 안전하지만
            // ActorNumber가 의미 없을 수 있으므로 가드.
            bool isYou = PhotonNetwork.InRoom
                         && PhotonNetwork.LocalPlayer != null
                         && player.ActorNumber == PhotonNetwork.LocalPlayer.ActorNumber;

            if (nameText != null)
            {
                var nick = string.IsNullOrEmpty(player.NickName) ? $"Player {player.ActorNumber}" : player.NickName;
                nameText.text = isYou ? $"{nick} (You)" : nick;
            }

            if (roleText != null)
            {
                roleText.text = player.IsMasterClient ? "Host" : "Client";
            }

            if (characterText != null)
            {
                string charStr = "-";
                if (NetworkManager.Instance != null
                    && NetworkManager.Instance.TryGetCharacterId(player, out int id))
                {
                    charStr = id.ToString();
                }
                characterText.text = $"Char: {charStr}";
            }

            if (readyText != null)
            {
                bool ready = NetworkManager.Instance != null
                             && NetworkManager.Instance.IsPlayerReady(player);
                readyText.text = ready ? "준비" : "대기";
            }

            if (kickButton != null)
            {
                // 로컬이 마스터이고, 대상이 본인이 아닐 때만 Kick 활성.
                bool canKick = PhotonNetwork.IsMasterClient && !player.IsLocal;
                kickButton.gameObject.SetActive(canKick);
                kickButton.interactable = canKick;
            }
        }

        public void Clear()
        {
            boundPlayer = null;
            if (nameText != null) nameText.text = string.Empty;
            if (roleText != null) roleText.text = string.Empty;
            if (characterText != null) characterText.text = string.Empty;
            if (readyText != null) readyText.text = string.Empty;
            if (kickButton != null) kickButton.gameObject.SetActive(false);
        }

        private void OnClickKick()
        {
            if (boundPlayer == null) return;
            NetworkManager.Instance?.KickPlayer(boundPlayer);
        }
    }
}
