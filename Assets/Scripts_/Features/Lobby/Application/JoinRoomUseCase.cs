using Features.Lobby.Application.Events;
using Features.Lobby.Application.Ports;
using Features.Lobby.Domain;
using Shared.EventBus;
using Shared.Kernel;

namespace Features.Lobby.Application
{
    public sealed class JoinRoomUseCase
    {
        private readonly ILobbyRepository _repository;
        private readonly ILobbyRoomNetworkPort _network;
        private readonly IClockPort _clock;
        private readonly IEventPublisher _eventBus;

        public JoinRoomUseCase(ILobbyRepository repository, ILobbyRoomNetworkPort network, IClockPort clock, IEventPublisher eventBus)
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

            var addResult = room.AddMember(member);
            if (addResult.IsFailure)
                return Fail(addResult.Error);

            var networkResult = _network.JoinRoom(roomId, member);
            if (networkResult.IsFailure)
                return Fail(networkResult.Error);

            var saveResult = _repository.SaveLobby(lobby);
            if (saveResult.IsFailure)
                return Fail(saveResult.Error);

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
