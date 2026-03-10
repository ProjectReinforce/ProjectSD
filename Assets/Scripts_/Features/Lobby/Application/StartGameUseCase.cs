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
                return LobbyCallbackHelper.Fail(_eventBus, "Room was not found.");

            var ruleResult = LobbyRule.CanStartGame(room);
            if (ruleResult.IsFailure)
                return LobbyCallbackHelper.Fail(_eventBus, ruleResult.Error);

            var networkResult = _network.RequestStartGame(roomId);
            if (networkResult.IsFailure)
                return LobbyCallbackHelper.Fail(_eventBus, networkResult.Error);

            return Result.Success();
        }
    }
}
