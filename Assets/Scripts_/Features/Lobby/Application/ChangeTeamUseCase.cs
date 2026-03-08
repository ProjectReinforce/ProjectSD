using Features.Lobby.Application.Events;
using Features.Lobby.Application.Ports;
using Features.Lobby.Domain;
using Shared.EventBus;
using Shared.Kernel;

namespace Features.Lobby.Application
{
    public sealed class ChangeTeamUseCase
    {
        private readonly ILobbyRepository _repository;
        private readonly ILobbyTeamNetworkPort _network;
        private readonly IEventPublisher _eventBus;

        public ChangeTeamUseCase(ILobbyRepository repository, ILobbyTeamNetworkPort network, IEventPublisher eventBus)
        {
            _repository = repository;
            _network = network;
            _eventBus = eventBus;
        }

        public Result Execute(EntityId roomId, EntityId memberId, TeamType team)
        {
            var lobby = _repository.LoadLobby();
            var room = lobby.FindRoom(roomId);
            if (room == null)
                return Fail("Room was not found.");

            var changeResult = room.ChangeTeam(memberId, team);
            if (changeResult.IsFailure)
                return Fail(changeResult.Error);

            var networkResult = _network.ChangeTeam(roomId, memberId, team);
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
