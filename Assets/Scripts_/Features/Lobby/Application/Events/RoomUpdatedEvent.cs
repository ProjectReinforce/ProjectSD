using Features.Lobby.Domain;
using Shared.Kernel;

namespace Features.Lobby.Application.Events
{
    public readonly struct RoomUpdatedEvent
    {
        public RoomSnapshot Room { get; }
        public DomainEntityId LocalMemberId { get; }

        public RoomUpdatedEvent(Room room, DomainEntityId localMemberId)
        {
            Room = new RoomSnapshot(room);
            LocalMemberId = localMemberId;
        }
    }
}
