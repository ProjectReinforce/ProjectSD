using Features.Player.Application;
using Features.Player.Domain;
using Features.Player.Infrastructure;
using Features.Player.Presentation;
using Shared.EventBus;
using Shared.Time;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Features.Player.Bootstrap
{
    public sealed class PlayerSetup : MonoBehaviour
    {
        [SerializeField] private PlayerNetworkAdapter _networkAdapter;
        [SerializeField] private PlayerMotorAdapter _motorAdapter;
        [SerializeField] private PlayerInputHandler _inputHandler;
        [SerializeField] private PlayerInput _playerInput;
        [SerializeField] private PlayerView _view;

        private void Start()
        {
            if (_networkAdapter == null)
            {
                Debug.LogError("[PlayerSetup] PlayerNetworkAdapter is missing.");
                return;
            }

            var eventBus = new EventBus();
            var _ = new PlayerNetworkEventHandler(eventBus, _networkAdapter);

            if (_networkAdapter.IsMine)
                InitializeLocal(eventBus);
            else
                InitializeRemote();
        }

        private void InitializeLocal(EventBus eventBus)
        {
            if (_motorAdapter == null)
            {
                Debug.LogError("[PlayerSetup] PlayerMotorAdapter is missing.");
                return;
            }

            var clock = new ClockAdapter();
            var useCases = new PlayerUseCases(_motorAdapter, _networkAdapter, clock);

            var spawnResult = useCases.Spawn(
                new PlayerSpec(walkSpeed: 5f, sprintMultiplier: 1.8f, jumpForce: 8f, gravity: 20f)
            );

            if (spawnResult.IsFailure)
            {
                Debug.LogError($"[PlayerSetup] Spawn failed: {spawnResult.Error}");
                return;
            }

            var player = spawnResult.Value;

            if (_inputHandler != null)
                _inputHandler.Initialize(player, useCases);

            if (_view != null)
                _view.Initialize(true, eventBus);
        }

        private void InitializeRemote()
        {
            if (_inputHandler != null)
                _inputHandler.enabled = false;

            if (_playerInput != null)
                _playerInput.enabled = false;

            if (_motorAdapter != null)
                _motorAdapter.enabled = false;

            if (_view != null)
                _view.Initialize(false, new EventBus());
        }
    }
}
