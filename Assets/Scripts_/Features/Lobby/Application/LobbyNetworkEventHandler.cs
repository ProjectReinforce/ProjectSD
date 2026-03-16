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

        public LobbyNetworkEventHandler(
            ILobbyRepository repository,
            IEventPublisher publisher,
            ILobbyNetworkCallbackPort networkEvents
        )
        {
            _repository = repository;
            _publisher = publisher;

            networkEvents.OnErrorOccurred = HandleError;
            networkEvents.OnCreateRoomSucceeded = HandleCreateRoomSucceeded;
            networkEvents.OnJoinRoomSucceeded = HandleJoinRoomSucceeded;
            networkEvents.OnLeaveRoomSucceeded = HandleLeaveRoomSucceeded;
            networkEvents.OnRemotePlayerEntered = HandleRemotePlayerEntered;
            networkEvents.OnRemotePlayerLeft = HandleRemotePlayerLeft;
            networkEvents.OnPlayerPropertiesChanged = HandlePlayerPropertiesChanged;
            networkEvents.OnGameStarted = HandleGameStarted;
        }

        private void HandleError(string message)
        {
            _publisher.Publish(new LobbyErrorEvent(message));
        }

        public Result HandleCreateRoomSucceeded(Room room)
        {
            return AddRoomAndPublish(room);
        }

        public Result HandleJoinRoomSucceeded(JoinRoomData data)
        {
            if (data.Members == null || data.Members.Count == 0)
                return Result.Failure("No members provided.");

            var owner = data.Members.Find(m => m.Id.Equals(data.MasterMemberId)) ?? data.Members[0];

            var roomResult = Room.Create(data.RoomId, data.RoomName, data.Capacity, owner);
            if (roomResult.IsFailure)
                return Result.Failure(roomResult.Error);

            var room = roomResult.Value;
            foreach (var member in data.Members)
            {
                if (member.Id.Equals(owner.Id))
                    continue;
                room.AddMember(member);
            }

            return AddRoomAndPublish(room);
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

        public Result HandlePlayerPropertiesChanged(PlayerPropertiesData data)
        {
            var lobby = _repository.LoadLobby();
            var room = lobby.FindRoom(data.RoomId);
            if (room == null)
                return Result.Failure("Room was not found.");

            if (data.Team.HasValue)
            {
                var changeResult = room.ChangeTeam(data.MemberId, data.Team.Value);
                if (changeResult.IsFailure)
                    return changeResult;
            }

            if (data.IsReady.HasValue)
            {
                var readyResult = room.SetReady(data.MemberId, data.IsReady.Value);
                if (readyResult.IsFailure)
                    return readyResult;
            }

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

            _publisher.Publish(new RoomUpdatedEvent(room));
            return Result.Success();
        }
    }
}
