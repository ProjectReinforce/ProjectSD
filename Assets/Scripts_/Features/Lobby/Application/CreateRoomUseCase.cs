using Features.Lobby.Application.Ports;
using Shared.Time;
using Features.Lobby.Domain;
using Shared.Kernel;

namespace Features.Lobby.Application
{
    public sealed class CreateRoomUseCase
    {
        private readonly ILobbyRepository _repository;
        private readonly ILobbyNetworkPort _network;
        private readonly IClockPort _clock;

        public CreateRoomUseCase(ILobbyRepository repository, ILobbyNetworkPort network, IClockPort clock)
        {
            _repository = repository;
            _network = network;
            _clock = clock;
        }

        public Result Execute(string roomName, int capacity, string ownerDisplayName)
        {
            var lobby = _repository.LoadLobby();

            var roomNameValidation = LobbyRule.ValidateRoomName(roomName);
            if (roomNameValidation.IsFailure)
                return Result.Failure(roomNameValidation.Error);

            var uniqueRoomValidation = LobbyRule.EnsureUniqueRoomName(lobby, roomName);
            if (uniqueRoomValidation.IsFailure)
                return Result.Failure(uniqueRoomValidation.Error);

            var ownerName = string.IsNullOrWhiteSpace(ownerDisplayName) ? "Host" : ownerDisplayName.Trim();
            var owner = new RoomMember(_clock.NewId(), ownerName, TeamType.None, false);

            var roomResult = Room.Create(_clock.NewId(), roomName.Trim(), capacity, owner);
            if (roomResult.IsFailure)
                return Result.Failure(roomResult.Error);

            return _network.CreateRoom(roomResult.Value);
        }
    }
}
