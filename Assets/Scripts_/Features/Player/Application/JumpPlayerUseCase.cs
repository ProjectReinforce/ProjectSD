using Features.Player.Application.Events;
using Shared.EventBus;
using Shared.Kernel;

namespace Features.Player.Application
{
    public sealed class JumpPlayerUseCase
    {
        private readonly IEventPublisher _eventBus;

        public JumpPlayerUseCase(IEventPublisher eventBus)
        {
            _eventBus = eventBus;
        }

        public Result Execute(Domain.Player player)
        {
            if (!player.TryJump())
                return Result.Failure("Cannot jump while airborne.");

            _eventBus.Publish(new PlayerJumpedEvent(player.Id));
            return Result.Success();
        }
    }
}
