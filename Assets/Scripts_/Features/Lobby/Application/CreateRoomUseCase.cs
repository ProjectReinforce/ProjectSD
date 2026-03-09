using Features.Lobby.Application.Events;
using Features.Lobby.Application.Ports;
using Shared.Time;
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
        private readonly IEventPublisher _eventBus;

        public CreateRoomUseCase(ILobbyRepository repository, ILobbyNetworkPort network, IClockPort clock, IEventPublisher eventBus)
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
            var networkResult = _network.RequestCreateRoom(room,
                onSuccess: () =>
                {
                    var currentLobby = _repository.LoadLobby();
                    var addResult = currentLobby.AddRoom(room);
                    if (addResult.IsFailure) { _eventBus.Publish(new LobbyErrorEvent(addResult.Error)); return; }

                    var saveResult = _repository.SaveLobby(currentLobby);
                    if (saveResult.IsFailure) { _eventBus.Publish(new LobbyErrorEvent(saveResult.Error)); return; }

                    _eventBus.Publish(new LobbyUpdatedEvent(currentLobby));
                    _eventBus.Publish(new RoomUpdatedEvent(room));
                },
                onFailure: error => Fail(error));

            if (networkResult.IsFailure)
                return Fail(networkResult.Error);

            return Result.Success();
        }

        private Result Fail(string message)
        {
            _eventBus.Publish(new LobbyErrorEvent(message));
            return Result.Failure(message);
        }
    }
}
