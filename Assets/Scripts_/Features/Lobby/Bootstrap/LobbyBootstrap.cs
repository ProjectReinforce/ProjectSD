using Features.Lobby.Application;
using Features.Lobby.Infrastructure;
using Features.Lobby.Infrastructure.Persistence;
using Features.Lobby.Infrastructure.Photon;
using Features.Lobby.Presentation;
using UnityEngine;

namespace Features.Lobby.Bootstrap
{
    public sealed class LobbyBootstrap : MonoBehaviour
    {
        [SerializeField] private LobbyEntryPoint _entryPoint;

        private void Awake()
        {
            if (_entryPoint == null)
            {
                Debug.LogError("[Lobby] LobbyEntryPoint reference is missing.");
                return;
            }

            if (_entryPoint.View == null)
            {
                Debug.LogError("[Lobby] LobbyView reference is missing in LobbyEntryPoint.");
                return;
            }

            var repository = new LobbyRepository();
            var network = new LobbyPhotonAdapter();
            var clock = new ClockAdapter();

            var presenter = new LobbyPresenter(
                _entryPoint.View,
                new CreateRoomUseCase(repository, network, clock),
                new JoinRoomUseCase(repository, network, clock),
                new LeaveRoomUseCase(repository, network),
                new ChangeTeamUseCase(repository, network),
                new SetReadyUseCase(repository, network),
                new StartGameUseCase(repository, network));

            _entryPoint.Initialize(presenter, repository.LoadLobby());
        }
    }
}
