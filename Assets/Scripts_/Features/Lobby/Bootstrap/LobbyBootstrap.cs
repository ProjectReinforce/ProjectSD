using Features.Lobby.Application;
using Features.Lobby.Infrastructure;
using Features.Lobby.Infrastructure.Persistence;
using Features.Lobby.Infrastructure.Photon;
using Features.Lobby.Presentation;
using Shared.EventBus;
using UnityEngine;

using DomainLobby = Features.Lobby.Domain.Lobby;

namespace Features.Lobby.Bootstrap
{
    public sealed class LobbyBootstrap : MonoBehaviour
    {
        [SerializeField] private LobbyView _view;

        private void Awake()
        {
            if (_view == null)
            {
                Debug.LogError("[Lobby] LobbyView reference is missing.");
                return;
            }

            var eventBus   = new EventBus();
            var repository = new LobbyRepository();
            var network    = new LobbyPhotonAdapter();
            var clock      = new ClockAdapter();

            var createRoom = new CreateRoomUseCase(repository, network, clock, eventBus);
            var joinRoom   = new JoinRoomUseCase(repository, network, clock, eventBus);
            var leaveRoom  = new LeaveRoomUseCase(repository, network, eventBus);
            var changeTeam = new ChangeTeamUseCase(repository, network, eventBus);
            var setReady   = new SetReadyUseCase(repository, network, eventBus);
            var startGame  = new StartGameUseCase(repository, network, eventBus);

            _view.Initialize(eventBus);
            _view.SetInputHandler(new LobbyInputHandler(createRoom, joinRoom, leaveRoom, changeTeam, setReady, startGame));
            _view.RenderLobby(repository.LoadLobby() ?? new DomainLobby());
        }
    }
}
