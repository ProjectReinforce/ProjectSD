using Features.Lobby.Application.Ports;
using Features.Lobby.Domain;
using Shared.Kernel;

namespace Features.Lobby.Application
{
    public sealed class JoinRoomUseCase
    {
        private readonly ILobbyRepository _repository;
        private readonly ILobbyNetworkPort _network;
        private readonly IClockPort _clock;

        public JoinRoomUseCase(ILobbyRepository repository, ILobbyNetworkPort network, IClockPort clock)
        {
            _repository = repository;
            _network = network;
            _clock = clock;
        }

        public Result Execute(EntityId roomId, string memberDisplayName, ILobbyOutputPort output)
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

            var name = string.IsNullOrWhiteSpace(memberDisplayName) ? "Player" : memberDisplayName.Trim();
            var member = new RoomMember(_clock.NewId(), name, TeamType.None, false);

            var addResult = room.AddMember(member);
            if (addResult.IsFailure)
            {
                return Fail(output, addResult.Error);
            }

            var networkResult = _network.JoinRoom(roomId, LobbyStateMapper.ToMemberState(member));
            if (networkResult.IsFailure)
            {
                return Fail(output, networkResult.Error);
            }

            var lobbyState = LobbyStateMapper.ToState(lobby);
            _repository.SaveLobby(lobbyState);
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
