using System;
using System.Collections.Generic;
using Shared.Kernel;

namespace Features.Lobby.Domain
{
    public sealed class Room : Entity
    {
        private readonly List<RoomMember> _members = new List<RoomMember>();

        private Room(DomainEntityId id, string name, int capacity, RoomMember owner)
            : base(id)
        {
            Name = name;
            Capacity = capacity;
            OwnerId = owner.Id;
            _members.Add(owner);
        }

        public string Name { get; }
        public int Capacity { get; }
        public DomainEntityId OwnerId { get; private set; }

        public IReadOnlyList<RoomMember> Members
        {
            get { return _members; }
        }

        public static Result<Room> Create(DomainEntityId id, string name, int capacity, RoomMember owner)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return Result<Room>.Failure("Room name is required.");
            }

            if (capacity < 1)
            {
                return Result<Room>.Failure("Capacity must be at least 1.");
            }

            if (owner == null)
            {
                return Result<Room>.Failure("Owner is required.");
            }

            return Result<Room>.Success(new Room(id, name.Trim(), capacity, owner));
        }

        public Result AddMember(RoomMember member)
        {
            if (member == null)
            {
                return Result.Failure("Member is required.");
            }

            if (_members.Count >= Capacity)
            {
                return Result.Failure("Room is full.");
            }

            if (FindMember(member.Id) != null)
            {
                return Result.Failure("Member already exists in room.");
            }

            _members.Add(member);
            return Result.Success();
        }

        public Result RemoveMember(DomainEntityId memberId)
        {
            var member = FindMember(memberId);
            if (member == null)
            {
                return Result.Failure("Member was not found.");
            }

            _members.Remove(member);
            if (OwnerId.Equals(memberId) && _members.Count > 0)
            {
                OwnerId = _members[0].Id;
            }

            return Result.Success();
        }

        public Result ChangeTeam(DomainEntityId memberId, TeamType team)
        {
            var member = FindMember(memberId);
            if (member == null)
            {
                return Result.Failure("Member was not found.");
            }

            member.ChangeTeam(team);
            return Result.Success();
        }

        public Result SetReady(DomainEntityId memberId, bool isReady)
        {
            var member = FindMember(memberId);
            if (member == null)
            {
                return Result.Failure("Member was not found.");
            }

            member.SetReady(isReady);
            return Result.Success();
        }

        public bool CanStartGame()
        {
            if (_members.Count < 1)
            {
                return false;
            }

            foreach (var member in _members)
            {
                if (!member.IsReady)
                {
                    return false;
                }
            }

            return true;
        }

        public RoomMember FindMember(DomainEntityId memberId)
        {
            return _members.Find(member => member.Id.Equals(memberId));
        }
    }
}
