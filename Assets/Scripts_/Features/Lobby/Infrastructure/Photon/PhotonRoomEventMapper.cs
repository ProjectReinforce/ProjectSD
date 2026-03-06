using Shared.Kernel;

namespace Features.Lobby.Infrastructure.Photon
{
    public sealed class PhotonRoomEventMapper
    {
        public EntityId MapRoomId(string roomId)
        {
            return new EntityId(roomId);
        }

        public EntityId MapMemberId(string memberId)
        {
            return new EntityId(memberId);
        }
    }
}
