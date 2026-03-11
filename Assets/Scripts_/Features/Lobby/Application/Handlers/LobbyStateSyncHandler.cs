using Features.Lobby.Application.Events;
using Features.Lobby.Application.Ports;
using Features.Lobby.Domain;
using Shared.EventBus;
using Shared.Kernel;

using DomainLobby = Features.Lobby.Domain.Lobby;

namespace Features.Lobby.Application.Handlers
{
    public sealed class LobbyStateSyncHandler
    {
        private readonly ILobbyRepository _repository;
        private readonly IEventPublisher _publisher;

        public LobbyStateSyncHandler(ILobbyRepository repository, IEventPublisher publisher)
        {
            _repository = repository;
            _publisher = publisher;
        }

        public void PublishError(string message)
        {
            _publisher.Publish(new LobbyErrorEvent(message));
        }

        public Result HandleCreateRoomSucceeded(Room room)
        {
            return AddRoomAndPublish(room);
        }

        public Result HandleJoinRoomSucceeded(Room room)
        {
            return AddRoomAndPublish(room);
        }

        private Result AddRoomAndPublish(Room room)
        {
            var lobby = _repository.LoadLobby();
            var addResult = lobby.AddRoom(room);
            if (addResult.IsFailure)
                return addResult;

            return SaveLobbyAndPublishRoom(lobby, room, publishLobbyUpdated: true);
        }

        public Result HandleLeaveRoomSucceeded(EntityId roomId, EntityId memberId)
        {
            var lobby = _repository.LoadLobby();
            var room = lobby.FindRoom(roomId);
            if (room == null)
                return Result.Failure("Room was not found.");

            var removeResult = room.RemoveMember(memberId);
            if (removeResult.IsFailure)
                return removeResult;

            if (room.Members.Count == 0)
            {
                var removeRoomResult = lobby.RemoveRoom(roomId);
                if (removeRoomResult.IsFailure)
                    return removeRoomResult;
            }

            var saveResult = _repository.SaveLobby(lobby);
            if (saveResult.IsFailure)
                return saveResult;

            _publisher.Publish(new LobbyUpdatedEvent(lobby));
            if (room.Members.Count > 0)
                _publisher.Publish(new RoomUpdatedEvent(room));

            return Result.Success();
        }

        public Result HandleRemotePlayerEntered(EntityId roomId, RoomMember member)
        {
            var lobby = _repository.LoadLobby();
            var room = lobby.FindRoom(roomId);
            if (room == null)
                return Result.Failure("Room was not found.");

            var addResult = room.AddMember(member);
            if (addResult.IsFailure)
                return addResult;

            return SaveLobbyAndPublishRoom(lobby, room, publishLobbyUpdated: false);
        }

        public Result HandleRemotePlayerLeft(EntityId roomId, EntityId memberId)
        {
            var lobby = _repository.LoadLobby();
            var room = lobby.FindRoom(roomId);
            if (room == null)
                return Result.Failure("Room was not found.");

            var removeResult = room.RemoveMember(memberId);
            if (removeResult.IsFailure)
                return removeResult;

            return SaveLobbyAndPublishRoom(lobby, room, publishLobbyUpdated: false);
        }

        public Result HandleTeamChanged(EntityId roomId, EntityId memberId, TeamType team)
        {
            var lobby = _repository.LoadLobby();
            var room = lobby.FindRoom(roomId);
            if (room == null)
                return Result.Failure("Room was not found.");

            var changeResult = room.ChangeTeam(memberId, team);
            if (changeResult.IsFailure)
                return changeResult;

            return SaveLobbyAndPublishRoom(lobby, room, publishLobbyUpdated: false);
        }

        public Result HandleReadyChanged(EntityId roomId, EntityId memberId, bool isReady)
        {
            var lobby = _repository.LoadLobby();
            var room = lobby.FindRoom(roomId);
            if (room == null)
                return Result.Failure("Room was not found.");

            var readyResult = room.SetReady(memberId, isReady);
            if (readyResult.IsFailure)
                return readyResult;

            return SaveLobbyAndPublishRoom(lobby, room, publishLobbyUpdated: false);
        }

        public Result HandleGameStarted(EntityId roomId)
        {
            var lobby = _repository.LoadLobby();
            var room = lobby.FindRoom(roomId);
            if (room == null)
                return Result.Failure("Room was not found.");

            _publisher.Publish(new GameStartedEvent(room));
            return Result.Success();
        }

        private Result SaveLobbyAndPublishRoom(DomainLobby lobby, Room room, bool publishLobbyUpdated)
        {
            var saveResult = _repository.SaveLobby(lobby);
            if (saveResult.IsFailure)
                return saveResult;

            if (publishLobbyUpdated)
                _publisher.Publish(new LobbyUpdatedEvent(lobby));

            _publisher.Publish(new RoomUpdatedEvent(room));
            return Result.Success();
        }
    }
}
