using System;
using Features.Combat.Application.Events;
using Features.Player.Application.Events;
using Shared.EventBus;

namespace Features.Player.Application
{
    public sealed class PlayerDamageEventHandler
    {
        private readonly Domain.Player _player;
        private readonly IEventPublisher _eventBus;

        public PlayerDamageEventHandler(
            Domain.Player player,
            IEventPublisher eventBus,
            IEventSubscriber subscriber)
        {
            _player = player;
            _eventBus = eventBus;
            subscriber.Subscribe(this, new Action<DamageAppliedEvent>(OnDamageApplied));
        }

        private void OnDamageApplied(DamageAppliedEvent e)
        {
            if (!_player.Id.Equals(e.TargetId))
                return;

            _eventBus.Publish(new PlayerHealthChangedEvent(
                _player.Id,
                _player.CurrentHp,
                _player.MaxHp,
                e.Damage,
                _player.IsDead
            ));

            if (e.IsDead)
            {
                _player.Die();
                _eventBus.Publish(new PlayerDiedEvent(_player.Id, e.AttackerId));
            }
        }
    }
}
