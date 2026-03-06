using Features.Lobby.Application.Ports;
using Features.Lobby.Domain;
using Shared.Kernel;

namespace Features.Lobby.Application
{
    public sealed class ChangeTeamUseCase
    {
        private readonly ILobbyRepository _repository;

        public ChangeTeamUseCase(ILobbyRepository repository)
        {
            _repository = repository;
        }

        public Result Execute(EntityId roomId, EntityId memberId, LobbyTeam team, ILobbyOutputPort output)
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

            var changeResult = room.ChangeTeam(memberId, LobbyStateMapper.ToDomainTeam(team));
            if (changeResult.IsFailure)
            {
                return Fail(output, changeResult.Error);
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
