using Features.Lobby.Application;
using UnityEngine;

namespace Features.Lobby.Presentation
{
    public sealed class LobbyEntryPoint : MonoBehaviour
    {
        [SerializeField] private LobbyView _view;

        private LobbyPresenter _presenter;
        private LobbyInputHandler _inputHandler;

        public LobbyInputHandler InputHandler
        {
            get { return _inputHandler; }
        }

        public LobbyView View
        {
            get { return _view; }
        }

        public void Initialize(LobbyPresenter presenter, LobbyState initialState)
        {
            if (_view == null)
            {
                Debug.LogError("[Lobby] LobbyView reference is missing.");
                return;
            }

            _presenter = presenter;
            _inputHandler = new LobbyInputHandler(presenter);
            presenter.ShowLobby(initialState ?? LobbyState.Empty);
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
