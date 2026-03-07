using Shared.Kernel;

namespace Features.Lobby.Infrastructure.Photon
{
    public sealed class PhotonRoomEventMapper
    {
        public EntityId MapId(string id)
        {
            return new EntityId(id);
        }
    }
}
