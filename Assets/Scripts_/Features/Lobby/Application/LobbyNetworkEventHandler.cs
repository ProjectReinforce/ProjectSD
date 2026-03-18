using Features.Lobby.Application.Events;
using Features.Lobby.Application.Ports;
using Features.Lobby.Domain;
using Shared.EventBus;
using Shared.Kernel;
using DomainLobby = Features.Lobby.Domain.Lobby;
using EntityId = Shared.Kernel.EntityId;

namespace Features.Lobby.Application
{
    public sealed class LobbyNetworkEventHandler
    {
        private readonly ILobbyRepository _repository;
        private readonly IEventPublisher _publisher;
        private EntityId _localMemberId;

        public LobbyNetworkEventHandler(
            ILobbyRepository repository,
            IEventPublisher publisher,
            ILobbyNetworkCallbackPort networkCallbacks
        )
        {
            _repository = repository;
            _publisher = publisher;

            networkCallbacks.OnErrorOccurred = HandleError;
            networkCallbacks.OnCreateRoomSucceeded = HandleCreateRoomSucceeded;
            networkCallbacks.OnJoinRoomSucceeded = HandleJoinRoomSucceeded;
            networkCallbacks.OnLeaveRoomSucceeded = HandleLeaveRoomSucceeded;
            networkCallbacks.OnRemotePlayerEntered = HandleRemotePlayerEntered;
            networkCallbacks.OnRemotePlayerLeft = HandleRemotePlayerLeft;
            networkCallbacks.OnPlayerPropertiesChanged = HandlePlayerPropertiesChanged;
            networkCallbacks.OnGameStarted = HandleGameStarted;
        }

        private void HandleError(string message)
        {
            _publisher.Publish(new LobbyErrorEvent(message));
        }

        private void HandleCreateRoomSucceeded(Room room)
        {
            _localMemberId = room.OwnerId;
            var result = AddRoomAndPublish(room);
            if (result.IsFailure)
                PublishError(result.Error);
        }

        private void HandleJoinRoomSucceeded(JoinRoomData data)
        {
            if (data.Members == null || data.Members.Count == 0)
            {
                PublishError("No members provided.");
                return;
            }

            _localMemberId = data.LocalMemberId;

            var owner = data.Members.Find(m => m.Id.Equals(data.MasterMemberId)) ?? data.Members[0];

            var roomResult = Room.Create(data.RoomId, data.RoomName, data.Capacity, owner);
            if (roomResult.IsFailure)
            {
                PublishError(roomResult.Error);
                return;
            }

            var room = roomResult.Value;
            foreach (var member in data.Members)
            {
                if (member.Id.Equals(owner.Id))
                    continue;
                room.AddMember(member);
            }

            var result = AddRoomAndPublish(room);
            if (result.IsFailure)
                PublishError(result.Error);
        }

        private void HandleLeaveRoomSucceeded(EntityId roomId, EntityId memberId)
        {
            var lobby = _repository.LoadLobby();
            var room = lobby.FindRoom(roomId);
            if (room == null)
            {
                PublishError("Room was not found.");
                return;
            }

            var removeResult = room.RemoveMember(memberId);
            if (removeResult.IsFailure)
            {
                PublishError(removeResult.Error);
                return;
            }

            if (room.Members.Count == 0)
            {
                var removeRoomResult = lobby.RemoveRoom(roomId);
                if (removeRoomResult.IsFailure)
                {
                    PublishError(removeRoomResult.Error);
                    return;
                }
            }

            var saveResult = _repository.SaveLobby(lobby);
            if (saveResult.IsFailure)
            {
                PublishError(saveResult.Error);
                return;
            }

            _publisher.Publish(new LobbyUpdatedEvent(lobby));
            if (room.Members.Count > 0)
                _publisher.Publish(new RoomUpdatedEvent(room, _localMemberId));
        }

        private void HandleRemotePlayerEntered(EntityId roomId, RoomMember member)
        {
            var lobby = _repository.LoadLobby();
            var room = lobby.FindRoom(roomId);
            if (room == null)
            {
                PublishError("Room was not found.");
                return;
            }

            var addResult = room.AddMember(member);
            if (addResult.IsFailure)
            {
                PublishError(addResult.Error);
                return;
            }

            var result = SaveLobbyAndPublishRoom(lobby, room, publishLobbyUpdated: false);
            if (result.IsFailure)
                PublishError(result.Error);
        }

        private void HandleRemotePlayerLeft(EntityId roomId, EntityId memberId)
        {
            var lobby = _repository.LoadLobby();
            var room = lobby.FindRoom(roomId);
            if (room == null)
            {
                PublishError("Room was not found.");
                return;
            }

            var removeResult = room.RemoveMember(memberId);
            if (removeResult.IsFailure)
            {
                PublishError(removeResult.Error);
                return;
            }

            var result = SaveLobbyAndPublishRoom(lobby, room, publishLobbyUpdated: false);
            if (result.IsFailure)
                PublishError(result.Error);
        }

        private void HandlePlayerPropertiesChanged(PlayerPropertiesData data)
        {
            var lobby = _repository.LoadLobby();
            var room = lobby.FindRoom(data.RoomId);
            if (room == null)
            {
                PublishError("Room was not found.");
                return;
            }

            if (data.Team.HasValue)
            {
                var changeResult = room.ChangeTeam(data.MemberId, data.Team.Value);
                if (changeResult.IsFailure)
                {
                    PublishError(changeResult.Error);
                    return;
                }
            }

            if (data.IsReady.HasValue)
            {
                var readyResult = room.SetReady(data.MemberId, data.IsReady.Value);
                if (readyResult.IsFailure)
                {
                    PublishError(readyResult.Error);
                    return;
                }
            }

            var result = SaveLobbyAndPublishRoom(lobby, room, publishLobbyUpdated: false);
            if (result.IsFailure)
                PublishError(result.Error);
        }

        private void HandleGameStarted(EntityId roomId)
        {
            var lobby = _repository.LoadLobby();
            var room = lobby.FindRoom(roomId);
            if (room == null)
            {
                PublishError("Room was not found.");
                return;
            }

            _publisher.Publish(new GameStartedEvent(room));
        }

        private Result AddRoomAndPublish(Room room)
        {
            var lobby = _repository.LoadLobby();
            var addResult = lobby.AddRoom(room);
            if (addResult.IsFailure)
                return addResult;

            return SaveLobbyAndPublishRoom(lobby, room, publishLobbyUpdated: true);
        }

        private Result SaveLobbyAndPublishRoom(
            DomainLobby lobby,
            Room room,
            bool publishLobbyUpdated
        )
        {
            var saveResult = _repository.SaveLobby(lobby);
            if (saveResult.IsFailure)
                return saveResult;

            if (publishLobbyUpdated)
                _publisher.Publish(new LobbyUpdatedEvent(lobby));

            _publisher.Publish(new RoomUpdatedEvent(room, _localMemberId));
            return Result.Success();
        }

        private void PublishError(string message)
        {
            _publisher.Publish(new LobbyErrorEvent(message));
        }
    }
}
