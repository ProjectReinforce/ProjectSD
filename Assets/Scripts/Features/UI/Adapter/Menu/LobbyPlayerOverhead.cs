using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using SwDreams.Shared.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SwDreams.Features.UI.Adapter.Menu
{
    /// <summary>
    /// 대기실 LobbyPlayer 자식 WorldSpaceCanvas에 부착되는 오버헤드 UI.
    ///
    /// 무엇: 캐릭터 머리 위에 떠 있는 NickName / Host 아이콘 / Ready 아이콘 표시.
    /// 왜:   플레이어 리스트(텍스트) 외에도 월드 공간에서 누가 누구인지 한눈에 식별해야 함.
    /// 어떻게: LobbyPlayerController(부모 PhotonView)의 Owner를 참조해 초기 바인딩.
    ///        OnPlayerPropertiesUpdate / OnMasterClientSwitched 콜백에서 즉시 갱신.
    /// </summary>
    public class LobbyPlayerOverhead : MonoBehaviourPunCallbacks
    {
        [Header("바인딩")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private GameObject hostIcon;
        [SerializeField] private GameObject readyIcon;

        [Header("캐릭터 ID (임시 표시)")]
        [Tooltip("캐릭터 스프라이트가 아직 없을 때 현재 characterId를 보여주는 임시 텍스트. 에셋 완성 후 제거 가능.")]
        [SerializeField] private TMP_Text characterIdText;
        [Tooltip("표시 포맷. {0}에 characterId가 들어감.")]
        [SerializeField] private string characterIdFormat = "Char {0}";

        [Header("선택 사항")]
        [Tooltip("본인 캐릭터의 NameText 색상 (optional).")]
        [SerializeField] private Color localColor = new Color(0.6f, 0.9f, 1f, 1f);
        [SerializeField] private Color remoteColor = Color.white;

        private PhotonView ownerView;

        private void Start()
        {
            // Start에서 탐색하는 이유:
            //   PhotonNetwork.Instantiate 직후 Awake 시점에는 부모 계층이 완성되지 않을 수 있다.
            //   Start는 첫 프레임 직전이라 루트 PhotonView가 확실히 붙어 있다.
            EnsureOwnerView();
            Refresh();
        }

        public override void OnEnable()
        {
            base.OnEnable();
            // Start보다 먼저 호출될 수 있으므로 방어적 탐색. null이면 Start가 이어받음.
            EnsureOwnerView();
            Refresh();
        }

        private void EnsureOwnerView()
        {
            if (ownerView == null)
            {
                ownerView = GetComponentInParent<PhotonView>();
            }
        }

        public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
        {
            if (ownerView == null || ownerView.Owner == null) return;
            if (targetPlayer == null || targetPlayer.ActorNumber != ownerView.Owner.ActorNumber) return;

            // isReady 변경만 UI에 의미 있지만, 과도한 최적화는 생략.
            Refresh();
        }

        public override void OnMasterClientSwitched(Player newMasterClient)
        {
            Refresh();
        }

        private void Refresh()
        {
            if (ownerView == null) return;
            var owner = ownerView.Owner;
            if (owner == null) return;

            if (nameText != null)
            {
                nameText.text = string.IsNullOrEmpty(owner.NickName)
                    ? $"Player {owner.ActorNumber}"
                    : owner.NickName;
                nameText.color = owner.IsLocal ? localColor : remoteColor;
            }

            if (hostIcon != null)
            {
                hostIcon.SetActive(owner.IsMasterClient);
            }

            if (readyIcon != null)
            {
                bool ready = NetworkManager.Instance != null
                             && NetworkManager.Instance.IsPlayerReady(owner);
                readyIcon.SetActive(ready);
            }

            if (characterIdText != null)
            {
                if (NetworkManager.Instance != null
                    && NetworkManager.Instance.TryGetCharacterId(owner, out int id))
                {
                    characterIdText.text = string.Format(characterIdFormat, id);
                }
                else
                {
                    characterIdText.text = string.Format(characterIdFormat, "-");
                }
            }
        }
    }
}
