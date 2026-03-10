using System.Collections.Generic;

using DomainLobby = Features.Lobby.Domain.Lobby;

namespace Features.Lobby.Application.Events
{
    public readonly struct LobbySnapshot
    {
        public IReadOnlyList<RoomSnapshot> Rooms { get; }

        public LobbySnapshot(DomainLobby lobby)
        {
            var rooms = new RoomSnapshot[lobby.Rooms.Count];
            for (var i = 0; i < lobby.Rooms.Count; i++)
                rooms[i] = new RoomSnapshot(lobby.Rooms[i]);
            Rooms = rooms;
        }
    }
}
