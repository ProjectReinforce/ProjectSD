using UnityEngine;

using DomainLobby = Features.Lobby.Domain.Lobby;

namespace Features.Lobby.Presentation
{
    public sealed class LobbyEntryPoint : MonoBehaviour
    {
        [SerializeField] private LobbyView _view;

        private LobbyPresenter _presenter;

        public LobbyView View
        {
            get { return _view; }
        }

        public void Initialize(LobbyPresenter presenter, DomainLobby initialLobby)
        {
            if (_view == null)
            {
                Debug.LogError("[Lobby] LobbyView reference is missing.");
                return;
            }

            _presenter = presenter;
            var inputHandler = new LobbyInputHandler(presenter);
            _view.SetInputHandler(inputHandler);
            presenter.ShowLobby(initialLobby ?? new DomainLobby());
        }

        private void Awake()
        {
            if (_presenter == null)
            {
                Debug.LogWarning("[Lobby] LobbyEntryPoint is not initialized. Add LobbyBootstrap in scene.");
            }
        }
    }
}
