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
    public sealed class PlayerBootstrap : MonoBehaviour
    {
        [SerializeField] private PlayerInputHandler _inputHandler;
        [SerializeField] private PlayerView _view;
        [SerializeField] private PlayerMotorAdapter _motorAdapter;
        [SerializeField] private PlayerNetworkAdapter _networkAdapter;

        private readonly EventBus _eventBus = new EventBus();

        private void Awake()
        {
            if (_networkAdapter == null)
                _networkAdapter = GetComponent<PlayerNetworkAdapter>();

            var isLocal = _networkAdapter != null && _networkAdapter.IsMine;

            if (isLocal)
                InitializeLocal();
            else
                InitializeRemote();
        }

        private void InitializeLocal()
        {
            if (_motorAdapter == null)
            {
                _motorAdapter = GetComponent<PlayerMotorAdapter>();
                if (_motorAdapter == null)
                {
                    Debug.LogError("[Player] PlayerMotorAdapter reference is missing.");
                    return;
                }
            }

            var clock = new ClockAdapter();
            var spec = new PlayerSpec(
                walkSpeed: 5f,
                sprintMultiplier: 1.8f,
                jumpForce: 8f,
                gravity: 20f
            );

            var spawnUseCase = new SpawnPlayerUseCase(clock, _eventBus);
            var spawnResult = spawnUseCase.Execute(spec);
            if (spawnResult.IsFailure)
            {
                Debug.LogError($"[Player] Spawn failed: {spawnResult.Error}");
                return;
            }

            var player = spawnResult.Value;

            var moveUseCase = new MovePlayerUseCase(_motorAdapter, _eventBus);
            var jumpUseCase = new JumpPlayerUseCase(_eventBus);

            if (_inputHandler != null)
                _inputHandler.Initialize(player, moveUseCase, jumpUseCase);

            if (_view != null)
                _view.Initialize(_eventBus);
        }

        private void InitializeRemote()
        {
            // 리모트 플레이어: 입력/물리 비활성화, 네트워크 위치 동기화만 사용
            if (_inputHandler != null)
                _inputHandler.enabled = false;

            var playerInput = GetComponent<PlayerInput>();
            if (playerInput != null)
                playerInput.enabled = false;

            if (_motorAdapter != null)
                _motorAdapter.enabled = false;
        }
    }
}
