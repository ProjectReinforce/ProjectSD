using UnityEngine;

namespace Features.Zone.Presentation
{
    public sealed class ZoneView : MonoBehaviour
    {
        [SerializeField] private float _fadeSpeed = 1f;

        private float _duration;
        private float _elapsed;
        private Renderer _renderer;

        public void Initialize(float radius, float duration)
        {
            _duration = duration;
            _elapsed = 0f;
            transform.localScale = new Vector3(radius * 2f, 0.1f, radius * 2f);

            _renderer = GetComponentInChildren<Renderer>();
        }

        public void SetColor(Color color)
        {
            if (_renderer != null)
                _renderer.material.color = color;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;

            if (_renderer != null)
            {
                var alpha = Mathf.Lerp(1f, 0f, _elapsed / _duration);
                var color = _renderer.material.color;
                color.a = alpha;
                _renderer.material.color = color;
            }

            if (_elapsed >= _duration)
                Destroy(gameObject);
        }
    }
}
