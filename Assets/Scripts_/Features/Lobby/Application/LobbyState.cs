using System.Collections.Generic;
using Shared.Kernel;

namespace Features.Lobby.Application
{
    public enum LobbyTeam
    {
        None = 0,
        Red = 1,
        Blue = 2
    }

    public sealed class MemberState
    {
        public MemberState(EntityId memberId, string displayName, LobbyTeam team, bool isReady)
        {
            MemberId = memberId;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Player" : displayName.Trim();
            Team = team;
            IsReady = isReady;
        }

        public EntityId MemberId { get; }
        public string DisplayName { get; }
        public LobbyTeam Team { get; }
        public bool IsReady { get; }
    }

    public sealed class RoomState
    {
        private readonly List<MemberState> _members;

        public RoomState(EntityId roomId, string name, int capacity, EntityId ownerId, List<MemberState> members)
        {
            RoomId = roomId;
            Name = string.IsNullOrWhiteSpace(name) ? "Room" : name.Trim();
            Capacity = capacity;
            OwnerId = ownerId;
            _members = members ?? new List<MemberState>();
        }

        public EntityId RoomId { get; }
        public string Name { get; }
        public int Capacity { get; }
        public EntityId OwnerId { get; }

        public IReadOnlyList<MemberState> Members
        {
            get { return _members; }
        }

        public MemberState FindMember(EntityId memberId)
        {
            return _members.Find(member => member.MemberId.Equals(memberId));
        }
    }

    public sealed class LobbyState
    {
        private static readonly LobbyState EmptyInstance = new LobbyState(new List<RoomState>());
        private readonly List<RoomState> _rooms;

        public LobbyState(List<RoomState> rooms)
        {
            _rooms = rooms ?? new List<RoomState>();
        }

        public static LobbyState Empty
        {
            get { return EmptyInstance; }
        }

        public IReadOnlyList<RoomState> Rooms
        {
            get { return _rooms; }
        }
    }
}
