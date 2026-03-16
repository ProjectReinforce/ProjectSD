using Features.Lobby.Application;
using Features.Lobby.Application.Events;
using Features.Lobby.Infrastructure.Persistence;
using Features.Lobby.Infrastructure.Photon;
using Features.Lobby.Presentation;
using Shared.Context;
using Shared.Time;
using UnityEngine;
using DomainLobby = Features.Lobby.Domain.Lobby;

public sealed class LobbyBootstrap : MonoBehaviour
{
    [SerializeField]
    private LobbyView _view;

    [SerializeField]
    private SceneContext _sceneContext;

    [SerializeField]
    private LobbyPhotonAdapter _photonAdapter;
    private LobbyNetworkEventHandler _syncHandler;

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

        var publisher = _sceneContext.Publisher;
        var subscriber = _sceneContext.Subscriber;
        var repository = new LobbyRepository();
        var network = _photonAdapter;
        var clock = new ClockAdapter();

        _syncHandler = new LobbyNetworkEventHandler(repository, publisher, network);

        var useCases = new LobbyUseCases(repository, network, clock);

        _view.Initialize(subscriber, useCases);
        publisher.Publish(new LobbyUpdatedEvent(repository.LoadLobby() ?? new DomainLobby()));
    }
}
