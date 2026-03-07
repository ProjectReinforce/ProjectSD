using Features.Lobby.Application.Ports;
using Shared.Kernel;

namespace Features.Lobby.Application
{
    public sealed class SetReadyUseCase
    {
        private readonly ILobbyRepository _repository;
        private readonly ILobbyNetworkPort _network;

        public SetReadyUseCase(ILobbyRepository repository, ILobbyNetworkPort network)
        {
            _repository = repository;
            _network = network;
        }

        public Result Execute(EntityId roomId, EntityId memberId, bool isReady, ILobbyOutputPort output)
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

            var readyResult = room.SetReady(memberId, isReady);
            if (readyResult.IsFailure)
            {
                return Fail(output, readyResult.Error);
            }

            var networkResult = _network.SetReady(roomId, memberId, isReady);
            if (networkResult.IsFailure)
            {
                return Fail(output, networkResult.Error);
            }

            var saveResult = _repository.SaveLobby(LobbyStateMapper.ToState(lobby));
            if (saveResult.IsFailure)
            {
                return Fail(output, saveResult.Error);
            }

            output.ShowRoom(LobbyStateMapper.ToRoomState(room));
            return Result.Success();
        }

        private static Result Fail(ILobbyOutputPort output, string message)
        {
            output.ShowError(message);
            return Result.Failure(message);
        }
    }
}
