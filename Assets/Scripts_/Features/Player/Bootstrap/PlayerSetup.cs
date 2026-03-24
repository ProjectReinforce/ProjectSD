using Features.Combat.Application.Ports;
using Features.Player.Application;
using Features.Player.Domain;
using Features.Player.Infrastructure;
using Features.Player.Presentation;
using Shared.EventBus;
using Shared.Kernel;
using Shared.Time;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Features.Player.Bootstrap
{
    public sealed class PlayerSetup : MonoBehaviour
    {
        [SerializeField]
        private PlayerNetworkAdapter _networkAdapter;

        [SerializeField]
        private PlayerMotorAdapter _motorAdapter;

        [SerializeField]
        private PlayerInputHandler _inputHandler;

        [SerializeField]
        private PlayerInput _playerInput;

        [SerializeField]
        private PlayerView _view;

        [SerializeField]
        private PlayerHealthHudView _healthHud;

        private PlayerUseCases _useCases;
        private PlayerCombatTargetProvider _combatTargetProvider;

        public ICombatTargetProvider CombatTargetProvider => _combatTargetProvider;
        public DomainEntityId PlayerId => _useCases?.LocalPlayer?.Id ?? default;
        public PlayerUseCases UseCases => _useCases;

        public void Initialize(EventBus eventBus)
        {
            Initialize(eventBus, null);
        }

        public void Initialize(EventBus eventBus, PlayerUseCases existingUseCases)
        {
            if (_networkAdapter == null)
            {
                Debug.LogError("[PlayerSetup] PlayerNetworkAdapter is missing.");
                return;
            }

            var _ = new PlayerNetworkEventHandler(eventBus, _networkAdapter);

            if (_networkAdapter.IsMine)
                InitializeLocal(eventBus, existingUseCases);
            else
                InitializeRemote(eventBus);
        }

        private void InitializeLocal(EventBus eventBus, PlayerUseCases existingUseCases)
        {
            if (_motorAdapter == null)
            {
                Debug.LogError("[PlayerSetup] PlayerMotorAdapter is missing.");
                return;
            }

            var clock = new ClockAdapter();

            if (existingUseCases != null)
            {
                _useCases = existingUseCases;
            }
            else
            {
                _useCases = new PlayerUseCases(_motorAdapter, _networkAdapter, eventBus, clock);
            }

            var spawnResult = _useCases.Spawn(
                new PlayerSpec(
                    walkSpeed: 5f,
                    sprintMultiplier: 1.8f,
                    jumpForce: 8f,
                    gravity: 20f,
                    maxHp: 100f,
                    defense: 5f
                )
            );

            if (spawnResult.IsFailure)
            {
                Debug.LogError($"[PlayerSetup] Spawn failed: {spawnResult.Error}");
                return;
            }

            var player = spawnResult.Value;

            _combatTargetProvider = new PlayerCombatTargetProvider(player);
            new PlayerDamageEventHandler(player, eventBus, eventBus);

            if (_inputHandler == null)
            {
                Debug.LogError(
                    "[PlayerSetup] PlayerInputHandler is not assigned in Inspector.",
                    this
                );
                return;
            }

            _inputHandler.Initialize(player, _useCases);

            if (_view == null)
            {
                Debug.LogError("[PlayerSetup] PlayerView is not assigned in Inspector.", this);
                return;
            }

            _view.Initialize(true, eventBus);

            if (_healthHud != null)
            {
                _healthHud.Initialize(eventBus, player.MaxHp);
            }
        }

        private void InitializeRemote(EventBus eventBus)
        {
            if (_inputHandler == null)
                Debug.LogError(
                    "[PlayerSetup] PlayerInputHandler is not assigned in Inspector.",
                    this
                );
            else
                _inputHandler.enabled = false;

            if (_playerInput == null)
                Debug.LogError("[PlayerSetup] PlayerInput is not assigned in Inspector.", this);
            else
                _playerInput.enabled = false;

            if (_motorAdapter == null)
                Debug.LogError(
                    "[PlayerSetup] PlayerMotorAdapter is not assigned in Inspector.",
                    this
                );
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
