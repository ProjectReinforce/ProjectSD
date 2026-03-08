using Features.Lobby.Application.Events;
using Features.Lobby.Application.Ports;
using Features.Lobby.Domain;
using Shared.EventBus;
using Shared.Kernel;

namespace Features.Lobby.Application
{
    public sealed class StartGameUseCase
    {
        private readonly ILobbyRepository _repository;
        private readonly ILobbyNetworkPort _network;
        private readonly IEventPublisher _eventBus;

        public StartGameUseCase(ILobbyRepository repository, ILobbyNetworkPort network, IEventPublisher eventBus)
        {
            _repository = repository;
            _network = network;
            _eventBus = eventBus;
        }

        public Result Execute(EntityId roomId)
        {
            var lobby = _repository.LoadLobby();
            var room = lobby.FindRoom(roomId);
            if (room == null)
                return Fail("Room was not found.");

            var ruleResult = LobbyRule.CanStartGame(room);
            if (ruleResult.IsFailure)
                return Fail(ruleResult.Error);

            var networkResult = _network.RequestStartGame(roomId);
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
