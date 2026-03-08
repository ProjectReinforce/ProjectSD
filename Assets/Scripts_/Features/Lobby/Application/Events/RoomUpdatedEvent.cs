using Features.Lobby.Domain;

namespace Features.Lobby.Application.Events
{
    public readonly struct RoomUpdatedEvent
    {
        public RoomUpdatedEvent(Room room) => Room = room;
        public Room Room { get; }
    }
}
