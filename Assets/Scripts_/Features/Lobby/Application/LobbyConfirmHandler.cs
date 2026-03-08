using Features.Lobby.Application.Events;
using Features.Lobby.Application.Ports;
using Shared.EventBus;

namespace Features.Lobby.Application
{
    public sealed class LobbyConfirmHandler
    {
        private readonly ILobbyRepository _repository;
        private readonly IEventPublisher _publisher;

        public LobbyConfirmHandler(ILobbyRepository repository, IEventPublisher publisher, IEventSubscriber subscriber)
        {
            _repository = repository;
            _publisher = publisher;

            subscriber.Subscribe<RoomCreatedEvent>(OnRoomCreated);
            subscriber.Subscribe<RoomJoinedEvent>(OnRoomJoined);
            subscriber.Subscribe<RoomLeftEvent>(OnRoomLeft);
        }

        private void OnRoomCreated(RoomCreatedEvent e)
        {
            var lobby = _repository.LoadLobby();
            var addResult = lobby.AddRoom(e.Room);
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
            _publisher.Publish(new RoomUpdatedEvent(e.Room));
        }

        private void OnRoomJoined(RoomJoinedEvent e)
        {
            var lobby = _repository.LoadLobby();
            var room = lobby.FindRoom(e.RoomId);
            if (room == null)
            {
                _publisher.Publish(new LobbyErrorEvent("Room was not found."));
                return;
            }

            var addResult = room.AddMember(e.Member);
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

        private void OnRoomLeft(RoomLeftEvent e)
        {
            var lobby = _repository.LoadLobby();
            var room = lobby.FindRoom(e.RoomId);
            if (room == null)
            {
                _publisher.Publish(new LobbyErrorEvent("Room was not found."));
                return;
            }

            var removeResult = room.RemoveMember(e.MemberId);
            if (removeResult.IsFailure)
            {
                _publisher.Publish(new LobbyErrorEvent(removeResult.Error));
                return;
            }

            if (room.Members.Count == 0)
            {
                lobby.RemoveRoom(e.RoomId);
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
    }
}
