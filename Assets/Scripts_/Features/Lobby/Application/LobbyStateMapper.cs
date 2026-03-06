using System.Collections.Generic;
using DomainLobby = Features.Lobby.Domain.Lobby;
using DomainRoom = Features.Lobby.Domain.Room;
using DomainRoomMember = Features.Lobby.Domain.RoomMember;
using DomainTeamType = Features.Lobby.Domain.TeamType;

namespace Features.Lobby.Application
{
    internal static class LobbyStateMapper
    {
        public static DomainLobby ToDomain(LobbyState state)
        {
            var lobby = new DomainLobby();
            if (state == null)
            {
                return lobby;
            }

            foreach (var roomState in state.Rooms)
            {
                if (roomState == null || roomState.Members.Count == 0)
                {
                    continue;
                }

                var ownerState = roomState.FindMember(roomState.OwnerId) ?? roomState.Members[0];
                var owner = new DomainRoomMember(
                    ownerState.MemberId,
                    ownerState.DisplayName,
                    ToDomainTeam(ownerState.Team),
                    ownerState.IsReady);

                var createRoomResult = DomainRoom.Create(roomState.RoomId, roomState.Name, roomState.Capacity, owner);
                if (createRoomResult.IsFailure)
                {
                    continue;
                }

                var room = createRoomResult.Value;
                for (var i = 0; i < roomState.Members.Count; i++)
                {
                    var member = roomState.Members[i];
                    if (member.MemberId.Equals(owner.Id))
                    {
                        continue;
                    }

                    room.AddMember(
                        new DomainRoomMember(
                            member.MemberId,
                            member.DisplayName,
                            ToDomainTeam(member.Team),
                            member.IsReady));
                }

                lobby.AddRoom(room);
            }

            return lobby;
        }

        public static LobbyState ToState(DomainLobby lobby)
        {
            if (lobby == null)
            {
                return LobbyState.Empty;
            }

            var rooms = new List<RoomState>();
            foreach (var room in lobby.Rooms)
            {
                rooms.Add(ToRoomState(room));
            }

            return new LobbyState(rooms);
        }

        public static RoomState ToRoomState(DomainRoom room)
        {
            var members = new List<MemberState>();
            foreach (var member in room.Members)
            {
                members.Add(ToMemberState(member));
            }

            return new RoomState(room.Id, room.Name, room.Capacity, room.OwnerId, members);
        }

        public static MemberState ToMemberState(DomainRoomMember member)
        {
            return new MemberState(member.Id, member.DisplayName, ToAppTeam(member.Team), member.IsReady);
        }

        public static DomainTeamType ToDomainTeam(LobbyTeam team)
        {
            switch (team)
            {
                case LobbyTeam.Red:
                    return DomainTeamType.Red;
                case LobbyTeam.Blue:
                    return DomainTeamType.Blue;
                default:
                    return DomainTeamType.None;
            }
        }

        public static LobbyTeam ToAppTeam(DomainTeamType team)
        {
            switch (team)
            {
                case DomainTeamType.Red:
                    return LobbyTeam.Red;
                case DomainTeamType.Blue:
                    return LobbyTeam.Blue;
                default:
                    return LobbyTeam.None;
            }
        }
    }
}
