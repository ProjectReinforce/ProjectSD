using Features.Lobby.Application.Ports;
using Shared.Time;
using Features.Lobby.Domain;
using Shared.EventBus;
using Shared.Kernel;

namespace Features.Lobby.Application
{
    public sealed class JoinRoomUseCase
    {
        private readonly ILobbyRepository _repository;
        private readonly ILobbyNetworkPort _network;
        private readonly IClockPort _clock;
        private readonly IEventPublisher _eventBus;

        public JoinRoomUseCase(ILobbyRepository repository, ILobbyNetworkPort network, IClockPort clock, IEventPublisher eventBus)
        {
            _repository = repository;
            _network = network;
            _clock = clock;
            _eventBus = eventBus;
        }

        public Result Execute(EntityId roomId, string memberDisplayName)
        {
            var lobby = _repository.LoadLobby();
            var room = lobby.FindRoom(roomId);
            if (room == null)
                return LobbyCallbackHelper.Fail(_eventBus, "Room was not found.");

            var name = string.IsNullOrWhiteSpace(memberDisplayName) ? "Player" : memberDisplayName.Trim();
            var member = new RoomMember(_clock.NewId(), name, TeamType.None, false);

            var onSuccess = LobbyCallbackHelper.CreateRoomCallback(
                _repository, _eventBus, roomId,
                (_, r) => r.AddMember(member),
                publishLobbyUpdated: true);

            var networkResult = _network.RequestJoinRoom(roomId, member,
                onSuccess: onSuccess,
                onFailure: error => LobbyCallbackHelper.Fail(_eventBus, error));

            if (networkResult.IsFailure)
                return LobbyCallbackHelper.Fail(_eventBus, networkResult.Error);

            return Result.Success();
        }
    }
}
