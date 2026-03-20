using System.Collections.Generic;
using Features.Lobby.Domain;
using Shared.Kernel;

namespace Features.Lobby.Application.Events
{
    public readonly struct RoomMemberSnapshot
    {
        public DomainEntityId Id { get; }
        public string DisplayName { get; }
        public TeamType Team { get; }
        public bool IsReady { get; }

        public RoomMemberSnapshot(RoomMember member)
        {
            Id = member.Id;
            DisplayName = member.DisplayName;
            Team = member.Team;
            IsReady = member.IsReady;
        }
    }

    public readonly struct RoomSnapshot
    {
        public DomainEntityId Id { get; }
        public string Name { get; }
        public int Capacity { get; }
        public DomainEntityId OwnerId { get; }
        public IReadOnlyList<RoomMemberSnapshot> Members { get; }

        public RoomSnapshot(Room room)
        {
            Id = room.Id;
            Name = room.Name;
            Capacity = room.Capacity;
            OwnerId = room.OwnerId;
            var members = new RoomMemberSnapshot[room.Members.Count];
            for (var i = 0; i < room.Members.Count; i++)
                members[i] = new RoomMemberSnapshot(room.Members[i]);
            Members = members;
        }
    }
}
