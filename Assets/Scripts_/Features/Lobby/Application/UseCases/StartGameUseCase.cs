using Features.Lobby.Application.Ports;
using Features.Lobby.Domain;
using Shared.Kernel;

namespace Features.Lobby.Application.UseCases
{
    public sealed class StartGameUseCase
    {
        private readonly ILobbyRepository _repository;
        private readonly ILobbyNetworkPort _network;

        public StartGameUseCase(ILobbyRepository repository, ILobbyNetworkPort network)
        {
            _repository = repository;
            _network = network;
        }

        public Result Execute(EntityId roomId)
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
