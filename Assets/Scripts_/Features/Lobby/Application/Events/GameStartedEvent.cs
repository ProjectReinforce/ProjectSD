using Features.Lobby.Domain;

namespace Features.Lobby.Application.Events
{
    public readonly struct GameStartedEvent
    {
        public GameStartedEvent(Room room) => Room = room;
        public Room Room { get; }
    }
}
