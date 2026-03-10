using Features.Lobby.Domain;

namespace Features.Lobby.Application.Events
{
    public readonly struct GameStartedEvent
    {
        public GameStartedEvent(Room room) => Room = new RoomSnapshot(room);
        public RoomSnapshot Room { get; }
    }
}
