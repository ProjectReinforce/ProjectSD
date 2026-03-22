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

        public void Initialize(EventBus eventBus)
        {
            if (_networkAdapter == null)
            {
                Debug.LogError("[PlayerSetup] PlayerNetworkAdapter is missing.");
                return;
            }

            var _ = new PlayerNetworkEventHandler(eventBus, _networkAdapter);

            if (_networkAdapter.IsMine)
                InitializeLocal(eventBus);
            else
                InitializeRemote(eventBus);
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

            if (_inputHandler == null)
            {
                Debug.LogError("[PlayerSetup] PlayerInputHandler is not assigned in Inspector.", this);
                return;
            }

            _inputHandler.Initialize(player, useCases);

            if (_view == null)
            {
                Debug.LogError("[PlayerSetup] PlayerView is not assigned in Inspector.", this);
                return;
            }

            _view.Initialize(true, eventBus);
        }

        private void InitializeRemote(EventBus eventBus)
        {
            if (_inputHandler == null)
                Debug.LogError("[PlayerSetup] PlayerInputHandler is not assigned in Inspector.", this);
            else
                _inputHandler.enabled = false;

            if (_playerInput == null)
                Debug.LogError("[PlayerSetup] PlayerInput is not assigned in Inspector.", this);
            else
                _playerInput.enabled = false;

            if (_motorAdapter == null)
                Debug.LogError("[PlayerSetup] PlayerMotorAdapter is not assigned in Inspector.", this);
            else
                _motorAdapter.enabled = false;

            if (_view == null)
            {
                Debug.LogError("[PlayerSetup] PlayerView is not assigned in Inspector.", this);
                return;
            }

            _view.Initialize(false, eventBus);
        }
    }
}
