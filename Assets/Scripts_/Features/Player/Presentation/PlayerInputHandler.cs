using Features.Player.Application;
using Shared.Math;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Features.Player.Presentation
{
    public sealed class PlayerInputHandler : MonoBehaviour
    {
        [SerializeField] private InputActionAsset _inputActions;

        private PlayerUseCases _useCases;
        private Domain.Player _player;

        private InputAction _moveAction;
        private InputAction _jumpAction;

        public void Initialize(Domain.Player player, PlayerUseCases useCases)
        {
            _player = player;
            _useCases = useCases;

            _moveAction = _inputActions.FindAction("Move");
            _jumpAction = _inputActions.FindAction("Jump");

            _moveAction.Enable();
            _jumpAction.Enable();

            _jumpAction.performed += OnJump;
        }

        private void OnDestroy()
        {
            if (_jumpAction != null)
            {
                _jumpAction.performed -= OnJump;
                _jumpAction.Disable();
            }

            if (_moveAction != null)
                _moveAction.Disable();
        }

        private void Update()
        {
            if (_player == null || _moveAction == null)
                return;

            var raw = _moveAction.ReadValue<Vector2>();
            var input = new Float2(raw.x, raw.y);
            _useCases.Move(_player, input, Time.deltaTime);
        }

        private void OnJump(InputAction.CallbackContext ctx)
        {
            if (_player == null)
                return;
            _useCases.Jump(_player);
        }
    }
}
