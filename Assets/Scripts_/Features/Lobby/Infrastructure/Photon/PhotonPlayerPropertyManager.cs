using ExitGames.Client.Photon;
using Features.Lobby.Domain;
using Photon.Pun;
using Shared.Kernel;

namespace Features.Lobby.Infrastructure.Photon
{
    /// <summary>
    /// Manages Photon local player custom properties.
    /// Single responsibility: interacting with Photon player properties.
    /// </summary>
    public sealed class PhotonPlayerPropertyManager
    {
        public bool SetLocalMemberProperties(RoomMember member)
        {
            if (PhotonNetwork.LocalPlayer == null)
                return false;

            var props = new Hashtable
            {
                [LobbyPhotonConstants.MemberIdKey]    = member.Id.Value,
                [LobbyPhotonConstants.DisplayNameKey] = member.DisplayName,
                [LobbyPhotonConstants.TeamKey]        = (int)member.Team,
                [LobbyPhotonConstants.IsReadyKey]     = member.IsReady
            };

            PhotonNetwork.LocalPlayer.NickName = member.DisplayName;
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
            return true;
        }

        public bool TryGetLocalMemberId(out DomainEntityId memberId)
        {
            memberId = default;

            if (PhotonNetwork.LocalPlayer == null)
                return false;
            if (!PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(LobbyPhotonConstants.MemberIdKey, out var value))
                return false;

            var raw = value as string;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            memberId = new DomainEntityId(raw);
            return true;
        }
    }
}
