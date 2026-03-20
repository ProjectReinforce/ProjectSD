using Features.Player.Application.Events;
using Features.Player.Domain;
using Shared.EventBus;
using Shared.Kernel;
using Shared.Time;

namespace Features.Player.Application
{
    public sealed class SpawnPlayerUseCase
    {
        private readonly IClockPort _clock;
        private readonly IEventPublisher _eventBus;

        public SpawnPlayerUseCase(IClockPort clock, IEventPublisher eventBus)
        {
            _clock = clock;
            _eventBus = eventBus;
        }

        public Result<Domain.Player> Execute(PlayerSpec spec)
        {
            var player = new Domain.Player(_clock.NewId(), spec);
            _eventBus.Publish(new PlayerSpawnedEvent(player.Id));
            return Result<Domain.Player>.Success(player);
        }
    }
}
