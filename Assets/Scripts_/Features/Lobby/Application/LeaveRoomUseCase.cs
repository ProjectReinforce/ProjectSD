using Features.Lobby.Application.Events;
using Features.Lobby.Application.Ports;
using Shared.EventBus;
using Shared.Kernel;

namespace Features.Lobby.Application
{
    public sealed class LeaveRoomUseCase
    {
        private readonly ILobbyRepository _repository;
        private readonly ILobbyNetworkPort _network;
        private readonly IEventPublisher _eventBus;

        public LeaveRoomUseCase(ILobbyRepository repository, ILobbyNetworkPort network, IEventPublisher eventBus)
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
                return LobbyCallbackHelper.Fail(_eventBus, "Room was not found.");

            if (room.FindMember(memberId) == null)
                return LobbyCallbackHelper.Fail(_eventBus, "Member was not found.");

            var networkResult = _network.RequestLeaveRoom(roomId, memberId,
                onSuccess: () =>
                {
                    var currentLobby = _repository.LoadLobby();
                    var currentRoom = currentLobby.FindRoom(roomId);
                    if (currentRoom == null) { _eventBus.Publish(new LobbyErrorEvent("Room was not found.")); return; }

                    var removeResult = currentRoom.RemoveMember(memberId);
                    if (removeResult.IsFailure) { _eventBus.Publish(new LobbyErrorEvent(removeResult.Error)); return; }

                    if (currentRoom.Members.Count == 0)
                        currentLobby.RemoveRoom(roomId);

                    var saveResult = _repository.SaveLobby(currentLobby);
                    if (saveResult.IsFailure) { _eventBus.Publish(new LobbyErrorEvent(saveResult.Error)); return; }

                    _eventBus.Publish(new LobbyUpdatedEvent(currentLobby));
                    if (currentRoom.Members.Count > 0)
                        _eventBus.Publish(new RoomUpdatedEvent(currentRoom));
                },
                onFailure: error => LobbyCallbackHelper.Fail(_eventBus, error));

            if (networkResult.IsFailure)
                return LobbyCallbackHelper.Fail(_eventBus, networkResult.Error);

            return Result.Success();
        }
    }
}
