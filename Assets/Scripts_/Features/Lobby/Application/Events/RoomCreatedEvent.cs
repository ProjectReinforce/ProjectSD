using Features.Lobby.Domain;

namespace Features.Lobby.Application.Events
{
    public readonly struct RoomCreatedEvent
    {
        public RoomCreatedEvent(Room room)
        {
            Room = room;
        }

        public Room Room { get; }
    }
}
