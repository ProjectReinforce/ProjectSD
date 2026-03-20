using Features.Player.Application.Events;
using Features.Player.Application.Ports;
using Shared.EventBus;
using Shared.Kernel;
using Shared.Math;

namespace Features.Player.Application
{
    public sealed class MovePlayerUseCase
    {
        private readonly IPlayerMotorPort _motor;
        private readonly IEventPublisher _eventBus;

        public MovePlayerUseCase(IPlayerMotorPort motor, IEventPublisher eventBus)
        {
            _motor = motor;
            _eventBus = eventBus;
        }

        public Result Execute(Domain.Player player, Float2 moveInput, float deltaTime)
        {
            var delta = player.CalculateMovement(moveInput, deltaTime);
            var result = _motor.Move(delta);

            player.SetGrounded(result.IsGrounded);
            player.UpdatePosition(result.Position);

            _eventBus.Publish(new PlayerMovedEvent(player.Id, result.Position));
            return Result.Success();
        }
    }
}
