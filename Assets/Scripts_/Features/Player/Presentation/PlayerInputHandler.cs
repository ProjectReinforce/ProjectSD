using Features.Player.Application;
using Shared.Math;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Features.Player.Presentation
{
    [RequireComponent(typeof(PlayerInput))]
    public sealed class PlayerInputHandler : MonoBehaviour
    {
        private MovePlayerUseCase _moveUseCase;
        private JumpPlayerUseCase _jumpUseCase;
        private Domain.Player _player;

        private InputAction _moveAction;
        private InputAction _jumpAction;
        private InputAction _sprintAction;

        public void Initialize(Domain.Player player, MovePlayerUseCase moveUseCase, JumpPlayerUseCase jumpUseCase)
        {
            _player = player;
            _moveUseCase = moveUseCase;
            _jumpUseCase = jumpUseCase;

            var playerInput = GetComponent<PlayerInput>();
            _moveAction = playerInput.actions["Move"];
            _jumpAction = playerInput.actions["Jump"];
            _sprintAction = playerInput.actions["Sprint"];

            _jumpAction.performed += OnJump;
            _sprintAction.started += OnSprintStarted;
            _sprintAction.canceled += OnSprintCanceled;
        }

        private void OnDestroy()
        {
            if (_jumpAction != null) _jumpAction.performed -= OnJump;
            if (_sprintAction != null)
            {
                _sprintAction.started -= OnSprintStarted;
                _sprintAction.canceled -= OnSprintCanceled;
            }
        }

        private void Update()
        {
            if (_player == null || _moveAction == null) return;

            var raw = _moveAction.ReadValue<Vector2>();
            var input = new Float2(raw.x, raw.y);
            _moveUseCase.Execute(_player, input, Time.deltaTime);
        }

        private void OnJump(InputAction.CallbackContext ctx)
        {
            if (_player == null) return;
            _jumpUseCase.Execute(_player);
        }

        private void OnSprintStarted(InputAction.CallbackContext ctx)
        {
            _player?.SetSprinting(true);
        }

        private void OnSprintCanceled(InputAction.CallbackContext ctx)
        {
            _player?.SetSprinting(false);
        }
    }
}
