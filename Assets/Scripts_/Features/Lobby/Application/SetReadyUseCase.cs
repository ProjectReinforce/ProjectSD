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

        public Result Execute(EntityId roomId, EntityId memberId, bool isReady)
        {
            var lobby = _repository.LoadLobby();
            var room = lobby.FindRoom(roomId);
            if (room == null)
                return Result.Failure("Room was not found.");

            if (room.FindMember(memberId) == null)
                return Result.Failure("Member was not found.");

            return _network.SetReady(memberId, isReady);
        }
    }
}
