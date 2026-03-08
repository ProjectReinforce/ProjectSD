using Features.Lobby.Application.Events;
using Features.Lobby.Application.Ports;
using Shared.EventBus;
using Shared.Kernel;

namespace Features.Lobby.Application
{
    public sealed class SetReadyUseCase
    {
        private readonly ILobbyRepository _repository;
        private readonly ILobbyNetworkPort _network;
        private readonly IEventPublisher _eventBus;

        public SetReadyUseCase(ILobbyRepository repository, ILobbyNetworkPort network, IEventPublisher eventBus)
        {
            _repository = repository;
            _network = network;
            _eventBus = eventBus;
        }

        public Result Execute(EntityId roomId, EntityId memberId, bool isReady)
        {
            var lobby = _repository.LoadLobby();
            var room = lobby.FindRoom(roomId);
            if (room == null)
                return Fail("Room was not found.");

            if (room.FindMember(memberId) == null)
                return Fail("Member was not found.");

            var networkResult = _network.RequestSetReady(roomId, memberId, isReady);
            if (networkResult.IsFailure)
                return Fail(networkResult.Error);

            return Result.Success();
        }

        private Result Fail(string message)
        {
            _eventBus.Publish(new LobbyErrorEvent(message));
            return Result.Failure(message);
        }
    }
}
