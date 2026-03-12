using Features.Lobby.Application.Events;
using Features.Lobby.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Features.Lobby.Presentation
{
    public sealed class MemberItemView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _teamText;
        [SerializeField] private Image _readyIcon;
        [SerializeField] private Color _readyColor = Color.green;
        [SerializeField] private Color _notReadyColor = Color.gray;

        public void Bind(RoomMemberSnapshot member)
        {
            _nameText.text = member.DisplayName;
            _teamText.text = member.Team == TeamType.None ? "-" : member.Team.ToString();
            _readyIcon.color = member.IsReady ? _readyColor : _notReadyColor;
        }
    }
}
