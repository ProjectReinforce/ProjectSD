using Features.Player.Application.Events;
using Features.Player.Application.Ports;
using Features.Player.Domain;
using Shared.EventBus;
using Shared.Kernel;
using Shared.Math;
using Shared.Time;

namespace Features.Player.Application
{
    public sealed class PlayerUseCases
    {
        private readonly IPlayerMotorPort _motor;
        private readonly IPlayerNetworkCommandPort _network;
        private readonly IEventPublisher _eventBus;
        private readonly IClockPort _clock;

        private Domain.Player _localPlayer;

        public PlayerUseCases(
            IPlayerMotorPort motor,
            IPlayerNetworkCommandPort network,
            IEventPublisher eventBus,
            IClockPort clock
        )
        {
            _motor = motor;
            _network = network;
            _eventBus = eventBus;
            _clock = clock;
        }

        public Result<Domain.Player> Spawn(PlayerSpec spec)
        {
            var player = new Domain.Player(_clock.NewId(), spec);
            _localPlayer = player;
            return Result<Domain.Player>.Success(player);
        }

        public Result Move(Domain.Player player, Float2 moveInput, float deltaTime)
        {
            var delta = player.CalculateMovement(moveInput, deltaTime);
            var result = _motor.Move(delta);

            player.ApplyMovement(result.Position, result.IsGrounded);
            return Result.Success();
        }

        public Result Jump(Domain.Player player)
        {
            if (!player.TryJump())
                return Result.Failure("Cannot jump while airborne.");

            _network.SendJump(player.Id);
            return Result.Success();
        }

        public Domain.Player LocalPlayer => _localPlayer;

        public void Respawn()
        {
            if (_localPlayer == null)
                return;

            _localPlayer.Respawn();
            _eventBus.Publish(new PlayerRespawnedEvent(
                _localPlayer.Id,
                _localPlayer.CurrentHp,
                _localPlayer.MaxHp
            ));
        }
    }
}
