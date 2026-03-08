using Features.Lobby.Application.Events;
using Features.Lobby.Application.Ports;
using Shared.EventBus;
using Shared.Kernel;

namespace Features.Lobby.Application
{
    public sealed class LeaveRoomUseCase
    {
        private readonly ILobbyRepository _repository;
        private readonly ILobbyRoomNetworkPort _network;
        private readonly IEventPublisher _eventBus;

        public LeaveRoomUseCase(ILobbyRepository repository, ILobbyRoomNetworkPort network, IEventPublisher eventBus)
        {
            _repository = repository;
            _network = network;
            _eventBus = eventBus;
        }

        public Result Execute(EntityId roomId, EntityId memberId)
        {
            var lobby = _repository.LoadLobby();
            var room = lobby.FindRoom(roomId);
            if (room == null)
                return Fail("Room was not found.");

            var leaveResult = room.RemoveMember(memberId);
            if (leaveResult.IsFailure)
                return Fail(leaveResult.Error);

            var networkResult = _network.LeaveRoom(roomId, memberId);
            if (networkResult.IsFailure)
                return Fail(networkResult.Error);

            if (room.Members.Count == 0)
                lobby.RemoveRoom(roomId);

            var saveResult = _repository.SaveLobby(lobby);
            if (saveResult.IsFailure)
                return Fail(saveResult.Error);

            _eventBus.Publish(new LobbyUpdatedEvent(lobby));
            return Result.Success();
        }

        private Result Fail(string message)
        {
            _eventBus.Publish(new LobbyErrorEvent(message));
            return Result.Failure(message);
        }
    }
}
