using Features.Lobby.Application.Events;
using Features.Lobby.Application.Ports;
using Features.Lobby.Domain;
using Shared.EventBus;
using Shared.Kernel;

namespace Features.Lobby.Application
{
    public sealed class CreateRoomUseCase
    {
        private readonly ILobbyRepository _repository;
        private readonly ILobbyNetworkPort _network;
        private readonly IClockPort _clock;
        private readonly IEventBus _eventBus;

        public CreateRoomUseCase(ILobbyRepository repository, ILobbyNetworkPort network, IClockPort clock, IEventBus eventBus)
        {
            _repository = repository;
            _network = network;
            _clock = clock;
            _eventBus = eventBus;
        }

        public Result Execute(string roomName, int capacity, string ownerDisplayName)
        {
            var lobby = _repository.LoadLobby();
            var roomNameValidation = LobbyRule.ValidateRoomName(roomName);
            if (roomNameValidation.IsFailure)
                return Fail(roomNameValidation.Error);

            var uniqueRoomValidation = LobbyRule.EnsureUniqueRoomName(lobby, roomName);
            if (uniqueRoomValidation.IsFailure)
                return Fail(uniqueRoomValidation.Error);

            var ownerName = string.IsNullOrWhiteSpace(ownerDisplayName) ? "Host" : ownerDisplayName.Trim();
            var owner = new RoomMember(_clock.NewId(), ownerName, TeamType.None, false);

            var roomResult = Room.Create(_clock.NewId(), roomName.Trim(), capacity, owner);
            if (roomResult.IsFailure)
                return Fail(roomResult.Error);

            var room = roomResult.Value;
            var addResult = lobby.AddRoom(room);
            if (addResult.IsFailure)
                return Fail(addResult.Error);

            var networkResult = _network.CreateRoom(room);
            if (networkResult.IsFailure)
                return Fail(networkResult.Error);

            var saveResult = _repository.SaveLobby(lobby);
            if (saveResult.IsFailure)
                return Fail(saveResult.Error);

            _eventBus.Publish(new LobbyUpdatedEvent(lobby));
            _eventBus.Publish(new RoomUpdatedEvent(room));
            return Result.Success();
        }

        private Result Fail(string message)
        {
            _eventBus.Publish(new LobbyErrorEvent(message));
            return Result.Failure(message);
        }
    }
}
