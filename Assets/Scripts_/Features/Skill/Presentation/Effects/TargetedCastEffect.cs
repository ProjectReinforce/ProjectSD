using UnityEngine;

namespace Features.Skill.Presentation
{
    public sealed class TargetedCastEffect : MonoBehaviour
    {
        [SerializeField] private float _duration = 0.5f;
        [SerializeField] private Color _flashColor = new Color(1f, 0.2f, 0.2f);

        private Renderer _renderer;
        private Color _originalColor;
        private float _elapsed;
        private bool _isFlashing;

        public void Play()
        {
            _renderer = GetComponentInChildren<Renderer>();
            if (_renderer == null)
            {
                Destroy(gameObject, _duration);
                return;
            }

            _originalColor = _renderer.material.color;
            _renderer.material.color = _flashColor;
            _elapsed = 0f;
            _isFlashing = true;
        }

        private void Update()
        {
            if (!_isFlashing) return;

            _elapsed += Time.deltaTime;
            if (_elapsed >= _duration)
            {
                if (_renderer != null)
                    _renderer.material.color = _originalColor;

                _isFlashing = false;
                Destroy(gameObject);
            }
        }
    }
}
