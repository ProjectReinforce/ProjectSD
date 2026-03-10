using Features.Lobby.Application.Ports;
using Features.Lobby.Domain;
using Shared.EventBus;
using Shared.Kernel;

namespace Features.Lobby.Application
{
    public sealed class ChangeTeamUseCase
    {
        private readonly ILobbyRepository _repository;
        private readonly ILobbyNetworkPort _network;
        private readonly IEventPublisher _eventBus;

        public ChangeTeamUseCase(ILobbyRepository repository, ILobbyNetworkPort network, IEventPublisher eventBus)
        {
            _repository = repository;
            _network = network;
            _eventBus = eventBus;
        }

        public Result Execute(EntityId roomId, EntityId memberId, TeamType team)
        {
            var lobby = _repository.LoadLobby();
            var room = lobby.FindRoom(roomId);
            if (room == null)
                return LobbyCallbackHelper.Fail(_eventBus, "Room was not found.");

            if (room.FindMember(memberId) == null)
                return LobbyCallbackHelper.Fail(_eventBus, "Member was not found.");

            var onSuccess = LobbyCallbackHelper.CreateRoomCallback(
                _repository, _eventBus, roomId,
                (_, r) => r.ChangeTeam(memberId, team),
                publishLobbyUpdated: false);

            var networkResult = _network.RequestChangeTeam(roomId, memberId, team,
                onSuccess: onSuccess,
                onFailure: error => LobbyCallbackHelper.Fail(_eventBus, error));

            if (networkResult.IsFailure)
                return LobbyCallbackHelper.Fail(_eventBus, networkResult.Error);

            return Result.Success();
        }
    }
}
