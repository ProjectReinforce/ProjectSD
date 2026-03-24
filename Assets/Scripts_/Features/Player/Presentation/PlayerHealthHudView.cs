using Features.Player.Application.Events;
using Shared.EventBus;
using UnityEngine;
using UnityEngine.UI;

namespace Features.Player.Presentation
{
    public sealed class PlayerHealthHudView : MonoBehaviour
    {
        [SerializeField]
        private Slider _healthSlider;

        [SerializeField]
        private Image _healthFillImage;

        [SerializeField]
        private Color _normalColor = Color.green;

        [SerializeField]
        private Color _lowColor = Color.red;

        [SerializeField]
        private float _lowHealthThreshold = 0.3f;

        private IEventSubscriber _eventBus;
        private float _maxHealth;

        private void Awake()
        {
            if (_healthSlider == null)
            {
                Debug.LogError("[PlayerHealthHudView] HealthSlider is not assigned.", this);
            }
        }

        public void Initialize(IEventSubscriber eventBus, float maxHealth)
        {
            _eventBus = eventBus;
            _maxHealth = maxHealth;

            _healthSlider.maxValue = maxHealth;
            _healthSlider.value = maxHealth;
            UpdateFillColor(1f);

            _eventBus.Subscribe(this, new System.Action<PlayerHealthChangedEvent>(OnHealthChanged));
            _eventBus.Subscribe(this, new System.Action<PlayerRespawnedEvent>(OnRespawned));
        }

        private void OnDestroy()
        {
            _eventBus?.UnsubscribeAll(this);
        }

        private void OnHealthChanged(PlayerHealthChangedEvent e)
        {
            if (_healthSlider == null)
                return;

            _maxHealth = e.MaxHp;
            _healthSlider.maxValue = e.MaxHp;
            _healthSlider.value = e.CurrentHp;

            var healthPercent = e.CurrentHp / e.MaxHp;
            UpdateFillColor(healthPercent);
        }

        private void OnRespawned(PlayerRespawnedEvent e)
        {
            if (_healthSlider == null)
                return;

            _maxHealth = e.MaxHp;
            _healthSlider.maxValue = e.MaxHp;
            _healthSlider.value = e.CurrentHp;

            UpdateFillColor(1f);
        }

        private void UpdateFillColor(float healthPercent)
        {
            if (_healthFillImage == null)
                return;

            _healthFillImage.color = healthPercent <= _lowHealthThreshold
                ? _lowColor
                : _normalColor;
        }
    }
}
