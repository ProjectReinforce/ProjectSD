using System;
using Features.Lobby.Application.Events;
using Features.Lobby.Application.Ports;
using Shared.EventBus;
using Shared.Kernel;

using DomainLobby = Features.Lobby.Domain.Lobby;
using DomainRoom = Features.Lobby.Domain.Room;

namespace Features.Lobby.Application
{
    internal static class LobbyCallbackHelper
    {
        internal static Result Fail(IEventPublisher eventBus, string message)
        {
            eventBus.Publish(new LobbyErrorEvent(message));
            return Result.Failure(message);
        }

        internal static Action CreateRoomCallback(
            ILobbyRepository repository,
            IEventPublisher eventBus,
            EntityId roomId,
            Func<DomainLobby, DomainRoom, Result> mutate,
            bool publishLobbyUpdated)
        {
            return () =>
            {
                var lobby = repository.LoadLobby();
                var room = lobby.FindRoom(roomId);
                if (room == null)
                {
                    eventBus.Publish(new LobbyErrorEvent("Room was not found."));
                    return;
                }

                var result = mutate(lobby, room);
                if (result.IsFailure)
                {
                    eventBus.Publish(new LobbyErrorEvent(result.Error));
                    return;
                }

                var saveResult = repository.SaveLobby(lobby);
                if (saveResult.IsFailure)
                {
                    eventBus.Publish(new LobbyErrorEvent(saveResult.Error));
                    return;
                }

                if (publishLobbyUpdated)
                    eventBus.Publish(new LobbyUpdatedEvent(lobby));
                eventBus.Publish(new RoomUpdatedEvent(room));
            };
        }
    }
}
