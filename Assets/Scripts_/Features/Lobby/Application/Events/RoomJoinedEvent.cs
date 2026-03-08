using Features.Lobby.Domain;
using Shared.Kernel;

namespace Features.Lobby.Application.Events
{
    public readonly struct RoomJoinedEvent
    {
        public RoomJoinedEvent(EntityId roomId, RoomMember member)
        {
            RoomId = roomId;
            Member = member;
        }

        public EntityId RoomId { get; }
        public RoomMember Member { get; }
    }
}
