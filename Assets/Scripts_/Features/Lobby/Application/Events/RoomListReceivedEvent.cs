using System.Collections.Generic;
using Features.Lobby.Application.Ports;

namespace Features.Lobby.Application.Events
{
    public readonly struct RoomListReceivedEvent
    {
        public IReadOnlyList<RoomListItem> Rooms { get; }

        public RoomListReceivedEvent(List<RoomListItem> rooms)
        {
            Rooms = rooms;
        }
    }
}
