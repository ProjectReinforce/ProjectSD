using Features.Lobby.Application;
using Features.Lobby.Application.Events;
using Features.Lobby.Infrastructure;
using Features.Lobby.Infrastructure.Persistence;
using Features.Lobby.Infrastructure.Photon;
using Features.Lobby.Presentation;
using Shared.Context;
using UnityEngine;

using DomainLobby = Features.Lobby.Domain.Lobby;

namespace Features.Lobby.Bootstrap
{
    public sealed class LobbyBootstrap : MonoBehaviour
    {
        [SerializeField] private LobbyView _view;
        [SerializeField] private SceneContext _sceneContext;
        [SerializeField] private LobbyPhotonAdapter _photonAdapter;

        private void Awake()
        {
            if (_view == null)
            {
                Debug.LogError("[Lobby] LobbyView reference is missing.");
                return;
            }

            if (_sceneContext == null)
            {
                Debug.LogError("[Lobby] SceneContext reference is missing.");
                return;
            }

            if (_photonAdapter == null)
            {
                _photonAdapter = GetComponent<LobbyPhotonAdapter>();
                if (_photonAdapter == null)
                {
                    Debug.LogError("[Lobby] LobbyPhotonAdapter reference is missing.");
                    return;
                }
            }

            var publisher  = _sceneContext.Publisher;
            var subscriber = _sceneContext.Subscriber;
            var repository = new LobbyRepository();
            var network    = _photonAdapter;
            var clock      = new ClockAdapter();

            var confirmHandler = new LobbyConfirmHandler(repository, publisher);
            network.Initialize(confirmHandler);

            var createRoom = new CreateRoomUseCase(repository, network, clock, publisher);
            var joinRoom   = new JoinRoomUseCase(repository, network, clock, publisher);
            var leaveRoom  = new LeaveRoomUseCase(repository, network, publisher);
            var changeTeam = new ChangeTeamUseCase(repository, network, publisher);
            var setReady   = new SetReadyUseCase(repository, network, publisher);
            var startGame  = new StartGameUseCase(repository, network, publisher);
            var inputHandler = new LobbyInputHandler(createRoom, joinRoom, leaveRoom, changeTeam, setReady, startGame);

            _view.Initialize(subscriber, inputHandler);
            publisher.Publish(new LobbyUpdatedEvent(repository.LoadLobby() ?? new DomainLobby()));
        }
    }
}
