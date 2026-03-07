using Features.Lobby.Application.Ports;
using Shared.Kernel;

namespace Features.Lobby.Application
{
    public sealed class LeaveRoomUseCase
    {
        private readonly ILobbyRepository _repository;
        private readonly ILobbyNetworkPort _network;

        public LeaveRoomUseCase(ILobbyRepository repository, ILobbyNetworkPort network)
        {
            _repository = repository;
            _network = network;
        }

        public Result Execute(EntityId roomId, EntityId memberId, ILobbyOutputPort output)
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

            var leaveResult = room.RemoveMember(memberId);
            if (leaveResult.IsFailure)
            {
                return Fail(output, leaveResult.Error);
            }

            var networkResult = _network.LeaveRoom(roomId, memberId);
            if (networkResult.IsFailure)
            {
                return Fail(output, networkResult.Error);
            }

            if (room.Members.Count == 0)
            {
                lobby.RemoveRoom(roomId);
            }

            var lobbyState = LobbyStateMapper.ToState(lobby);
            var saveResult = _repository.SaveLobby(lobbyState);
            if (saveResult.IsFailure)
            {
                return Fail(output, saveResult.Error);
            }

            output.ShowLobby(lobbyState);
            return Result.Success();
        }

        private static Result Fail(ILobbyOutputPort output, string message)
        {
            output.ShowError(message);
            return Result.Failure(message);
        }
    }
}
