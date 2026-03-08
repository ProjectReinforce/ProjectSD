using Features.Lobby.Application.Events;
using Features.Lobby.Application.Ports;
using Features.Lobby.Domain;
using Shared.EventBus;
using Shared.Kernel;

namespace Features.Lobby.Application
{
    public sealed class LobbyConfirmHandler
    {
        private readonly ILobbyRepository _repository;
        private readonly IEventPublisher _publisher;

        public LobbyConfirmHandler(ILobbyRepository repository, IEventPublisher publisher)
        {
            _repository = repository;
            _publisher = publisher;
        }

        public void OnRoomCreated(Room room)
        {
            var lobby = _repository.LoadLobby();
            var addResult = lobby.AddRoom(room);
            if (addResult.IsFailure)
            {
                _publisher.Publish(new LobbyErrorEvent(addResult.Error));
                return;
            }

            var saveResult = _repository.SaveLobby(lobby);
            if (saveResult.IsFailure)
            {
                _publisher.Publish(new LobbyErrorEvent(saveResult.Error));
                return;
            }

            _publisher.Publish(new LobbyUpdatedEvent(lobby));
            _publisher.Publish(new RoomUpdatedEvent(room));
        }

        public void OnRoomJoined(EntityId roomId, RoomMember member)
        {
            var lobby = _repository.LoadLobby();
            var room = lobby.FindRoom(roomId);
            if (room == null)
            {
                _publisher.Publish(new LobbyErrorEvent("Room was not found."));
                return;
            }

            var addResult = room.AddMember(member);
            if (addResult.IsFailure)
            {
                _publisher.Publish(new LobbyErrorEvent(addResult.Error));
                return;
            }

            var saveResult = _repository.SaveLobby(lobby);
            if (saveResult.IsFailure)
            {
                _publisher.Publish(new LobbyErrorEvent(saveResult.Error));
                return;
            }

            _publisher.Publish(new LobbyUpdatedEvent(lobby));
            _publisher.Publish(new RoomUpdatedEvent(room));
        }

        public void OnRoomLeft(EntityId roomId, EntityId memberId)
        {
            var lobby = _repository.LoadLobby();
            var room = lobby.FindRoom(roomId);
            if (room == null)
            {
                _publisher.Publish(new LobbyErrorEvent("Room was not found."));
                return;
            }

            var removeResult = room.RemoveMember(memberId);
            if (removeResult.IsFailure)
            {
                _publisher.Publish(new LobbyErrorEvent(removeResult.Error));
                return;
            }

            if (room.Members.Count == 0)
            {
                lobby.RemoveRoom(roomId);
            }

            var saveResult = _repository.SaveLobby(lobby);
            if (saveResult.IsFailure)
            {
                _publisher.Publish(new LobbyErrorEvent(saveResult.Error));
                return;
            }

            _publisher.Publish(new LobbyUpdatedEvent(lobby));
            if (room.Members.Count > 0)
            {
                _publisher.Publish(new RoomUpdatedEvent(room));
            }
        }

        public void OnTeamChanged(EntityId roomId, EntityId memberId, TeamType team)
        {
            var lobby = _repository.LoadLobby();
            var room = lobby.FindRoom(roomId);
            if (room == null)
            {
                _publisher.Publish(new LobbyErrorEvent("Room was not found."));
                return;
            }

            var changeResult = room.ChangeTeam(memberId, team);
            if (changeResult.IsFailure)
            {
                _publisher.Publish(new LobbyErrorEvent(changeResult.Error));
                return;
            }

            var saveResult = _repository.SaveLobby(lobby);
            if (saveResult.IsFailure)
            {
                _publisher.Publish(new LobbyErrorEvent(saveResult.Error));
                return;
            }

            _publisher.Publish(new RoomUpdatedEvent(room));
        }

        public void OnReadyChanged(EntityId roomId, EntityId memberId, bool isReady)
        {
            var lobby = _repository.LoadLobby();
            var room = lobby.FindRoom(roomId);
            if (room == null)
            {
                _publisher.Publish(new LobbyErrorEvent("Room was not found."));
                return;
            }

            var readyResult = room.SetReady(memberId, isReady);
            if (readyResult.IsFailure)
            {
                _publisher.Publish(new LobbyErrorEvent(readyResult.Error));
                return;
            }

            var saveResult = _repository.SaveLobby(lobby);
            if (saveResult.IsFailure)
            {
                _publisher.Publish(new LobbyErrorEvent(saveResult.Error));
                return;
            }

            _publisher.Publish(new RoomUpdatedEvent(room));
        }

        public void OnGameStarted(EntityId roomId)
        {
            var lobby = _repository.LoadLobby();
            var room = lobby.FindRoom(roomId);
            if (room == null)
            {
                _publisher.Publish(new LobbyErrorEvent("Room was not found."));
                return;
            }

            _publisher.Publish(new GameStartedEvent(room));
        }

        public void OnNetworkError(string error)
        {
            _publisher.Publish(new LobbyErrorEvent(error));
        }
    }
}
