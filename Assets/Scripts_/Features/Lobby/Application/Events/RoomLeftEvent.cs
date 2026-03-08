using Shared.Kernel;

namespace Features.Lobby.Application.Events
{
    public readonly struct RoomLeftEvent
    {
        public RoomLeftEvent(EntityId roomId, EntityId memberId)
        {
            RoomId = roomId;
            MemberId = memberId;
        }

        public EntityId RoomId { get; }
        public EntityId MemberId { get; }
    }
}
