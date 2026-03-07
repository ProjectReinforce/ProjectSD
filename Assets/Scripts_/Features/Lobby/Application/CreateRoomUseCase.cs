using Features.Lobby.Application.Ports;
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

        public Result Execute(string roomName, int capacity, string ownerDisplayName, ILobbyOutputPort output)
        {
            if (output == null)
            {
                return Result.Failure("Output port is required.");
            }

            var lobby = _repository.LoadLobby();
            var roomNameValidation = LobbyRule.ValidateRoomName(roomName);
            if (roomNameValidation.IsFailure)
            {
                return Fail(output, roomNameValidation.Error);
            }

            var uniqueRoomValidation = LobbyRule.EnsureUniqueRoomName(lobby, roomName);
            if (uniqueRoomValidation.IsFailure)
            {
                return Fail(output, uniqueRoomValidation.Error);
            }

            var ownerName = string.IsNullOrWhiteSpace(ownerDisplayName) ? "Host" : ownerDisplayName.Trim();
            var owner = new RoomMember(_clock.NewId(), ownerName, TeamType.None, false);

            var roomResult = Room.Create(_clock.NewId(), roomName.Trim(), capacity, owner);
            if (roomResult.IsFailure)
            {
                return Fail(output, roomResult.Error);
            }

            var room = roomResult.Value;
            var addResult = lobby.AddRoom(room);
            if (addResult.IsFailure)
            {
                return Fail(output, addResult.Error);
            }

            var networkResult = _network.CreateRoom(room);
            if (networkResult.IsFailure)
            {
                return Fail(output, networkResult.Error);
            }

            var saveResult = _repository.SaveLobby(lobby);
            if (saveResult.IsFailure)
            {
                return Fail(output, saveResult.Error);
            }

            output.ShowLobby(lobby);
            output.ShowRoom(room);
            return Result.Success();
        }

        private static Result Fail(ILobbyOutputPort output, string message)
        {
            output.ShowError(message);
            return Result.Failure(message);
        }
    }
}
