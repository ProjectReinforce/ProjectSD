using Features.Lobby.Application.Ports;
using Shared.Kernel;

namespace Features.Lobby.Application
{
    public sealed class SetReadyUseCase
    {
        private readonly ILobbyRepository _repository;

        public SetReadyUseCase(ILobbyRepository repository)
        {
            _repository = repository;
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

            _repository.SaveLobby(LobbyStateMapper.ToState(lobby));
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
