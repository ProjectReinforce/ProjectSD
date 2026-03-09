using Features.Lobby.Application.Events;
using Features.Lobby.Application.Ports;
using Shared.Time;
using Features.Lobby.Domain;
using Shared.EventBus;
using Shared.Kernel;

namespace Features.Lobby.Application
{
    public sealed class JoinRoomUseCase
    {
        private readonly ILobbyRepository _repository;
        private readonly ILobbyNetworkPort _network;
        private readonly IClockPort _clock;
        private readonly IEventPublisher _eventBus;

        public JoinRoomUseCase(ILobbyRepository repository, ILobbyNetworkPort network, IClockPort clock, IEventPublisher eventBus)
        {
            _repository = repository;
            _network = network;
            _clock = clock;
            _eventBus = eventBus;
        }

        public Result Execute(EntityId roomId, string memberDisplayName)
        {
            var lobby = _repository.LoadLobby();
            var room = lobby.FindRoom(roomId);
            if (room == null)
                return Fail("Room was not found.");

            var name = string.IsNullOrWhiteSpace(memberDisplayName) ? "Player" : memberDisplayName.Trim();
            var member = new RoomMember(_clock.NewId(), name, TeamType.None, false);

            var networkResult = _network.RequestJoinRoom(roomId, member,
                onSuccess: () =>
                {
                    var currentLobby = _repository.LoadLobby();
                    var currentRoom = currentLobby.FindRoom(roomId);
                    if (currentRoom == null) { _eventBus.Publish(new LobbyErrorEvent("Room was not found.")); return; }

                    var addResult = currentRoom.AddMember(member);
                    if (addResult.IsFailure) { _eventBus.Publish(new LobbyErrorEvent(addResult.Error)); return; }

                    var saveResult = _repository.SaveLobby(currentLobby);
                    if (saveResult.IsFailure) { _eventBus.Publish(new LobbyErrorEvent(saveResult.Error)); return; }

                    _eventBus.Publish(new LobbyUpdatedEvent(currentLobby));
                    _eventBus.Publish(new RoomUpdatedEvent(currentRoom));
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
