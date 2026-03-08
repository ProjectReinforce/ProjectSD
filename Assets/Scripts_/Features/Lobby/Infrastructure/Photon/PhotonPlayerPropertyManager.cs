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
        private const string MemberIdKey = "memberId";
        private const string TeamKey = "team";
        private const string IsReadyKey = "isReady";
        private const string DisplayNameKey = "displayName";

        public bool SetLocalMemberProperties(EntityId memberId, string displayName, TeamType team, bool isReady)
        {
            if (PhotonNetwork.LocalPlayer == null)
                return false;

            var displayNameValue = string.IsNullOrWhiteSpace(displayName) ? "Player" : displayName.Trim();
            var props = new Hashtable
            {
                [MemberIdKey] = memberId.Value,
                [DisplayNameKey] = displayNameValue,
                [TeamKey] = (int)team,
                [IsReadyKey] = isReady
            };

            PhotonNetwork.LocalPlayer.NickName = displayNameValue;
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
            return true;
        }

        public bool TryGetLocalMemberId(out EntityId memberId)
        {
            memberId = default;

            if (PhotonNetwork.LocalPlayer == null)
                return false;
            if (!PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(MemberIdKey, out var value))
                return false;

            var raw = value as string;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            memberId = new EntityId(raw);
            return true;
        }
    }
}
