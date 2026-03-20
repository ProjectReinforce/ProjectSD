using Features.Lobby.Application.Ports;
using Features.Lobby.Domain;
using Shared.Kernel;
using Shared.Time;

namespace Features.Lobby.Application
{
    public sealed class LobbyUseCases
    {
        private readonly ILobbyRepository _repository;
        private readonly ILobbyNetworkCommandPort _network;
        private readonly IClockPort _clock;

        public LobbyUseCases(
            ILobbyRepository repository,
            ILobbyNetworkCommandPort network,
            IClockPort clock
        )
        {
            _repository = repository;
            _network = network;
            _clock = clock;
        }

        public Result CreateRoom(string roomName, int capacity, string ownerDisplayName)
        {
            var lobby = _repository.LoadLobby();

            var roomNameValidation = LobbyRule.ValidateRoomName(roomName);
            if (roomNameValidation.IsFailure)
                return Result.Failure(roomNameValidation.Error);

            var uniqueRoomValidation = LobbyRule.EnsureUniqueRoomName(lobby, roomName);
            if (uniqueRoomValidation.IsFailure)
                return Result.Failure(uniqueRoomValidation.Error);

            var ownerName = string.IsNullOrWhiteSpace(ownerDisplayName)
                ? "Host"
                : ownerDisplayName.Trim();
            var owner = new RoomMember(_clock.NewId(), ownerName, TeamType.None, false);

            var roomResult = Room.Create(_clock.NewId(), roomName.Trim(), capacity, owner);
            if (roomResult.IsFailure)
                return Result.Failure(roomResult.Error);

            return _network.CreateRoom(roomResult.Value);
        }

        public Result JoinRoom(DomainEntityId roomId, string memberDisplayName)
        {
            var name = string.IsNullOrWhiteSpace(memberDisplayName)
                ? "Player"
                : memberDisplayName.Trim();
            var member = new RoomMember(_clock.NewId(), name, TeamType.None, false);
            return _network.JoinRoom(roomId, member);
        }

        public Result LeaveRoom(DomainEntityId roomId, DomainEntityId memberId)
        {
            var lobby = _repository.LoadLobby();
            var room = lobby.FindRoom(roomId);
            if (room == null)
                return Result.Failure("Room was not found.");

            if (room.FindMember(memberId) == null)
                return Result.Failure("Member was not found.");

            return _network.LeaveRoom(roomId, memberId);
        }

        public Result ChangeTeam(DomainEntityId roomId, DomainEntityId memberId, TeamType team)
        {
            var lobby = _repository.LoadLobby();
            var room = lobby.FindRoom(roomId);
            if (room == null)
                return Result.Failure("Room was not found.");

            if (room.FindMember(memberId) == null)
                return Result.Failure("Member was not found.");

            return _network.ChangeTeam(memberId, team);
        }

        public Result SetReady(DomainEntityId roomId, DomainEntityId memberId, bool isReady)
        {
            var lobby = _repository.LoadLobby();
            var room = lobby.FindRoom(roomId);
            if (room == null)
                return Result.Failure("Room was not found.");

            if (room.FindMember(memberId) == null)
                return Result.Failure("Member was not found.");

            return _network.SetReady(memberId, isReady);
        }

        public Result StartGame(DomainEntityId roomId)
        {
            var lobby = _repository.LoadLobby();
            var room = lobby.FindRoom(roomId);
            if (room == null)
                return Result.Failure("Room was not found.");

            var ruleResult = LobbyRule.CanStartGame(room);
            if (ruleResult.IsFailure)
                return Result.Failure(ruleResult.Error);

            return _network.StartGame(roomId);
        }
    }
}
