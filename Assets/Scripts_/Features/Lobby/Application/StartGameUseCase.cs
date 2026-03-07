using Features.Lobby.Application.Ports;
using Features.Lobby.Domain;
using Shared.Kernel;

namespace Features.Lobby.Application
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

        public Result Execute(EntityId roomId, ILobbyOutputPort output)
        {
            if (output == null)
            {
                return Result.Failure("Output port is required.");
            }

            var lobby = LobbyStateMapper.ToDomain(_repository.LoadLobby());
            var room = lobby.FindRoom(roomId);
            if (room == null)
            {
                return Fail(output, "Room was not found.");
            }

            var ruleResult = DomainRule.CanStartGame(room);
            if (ruleResult.IsFailure)
            {
                return Fail(output, ruleResult.Error);
            }

            var networkResult = _network.StartGame(roomId);
            if (networkResult.IsFailure)
            {
                return Fail(output, networkResult.Error);
            }

            var saveResult = _repository.SaveLobby(LobbyStateMapper.ToState(lobby));
            if (saveResult.IsFailure)
            {
                return Fail(output, saveResult.Error);
            }

            output.ShowStartGame(LobbyStateMapper.ToRoomState(room));
            return Result.Success();
        }

        private static Result Fail(ILobbyOutputPort output, string message)
        {
            output.ShowError(message);
            return Result.Failure(message);
        }
    }
}
