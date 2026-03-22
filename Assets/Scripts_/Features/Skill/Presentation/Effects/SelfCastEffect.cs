using UnityEngine;

namespace Features.Skill.Presentation
{
    public sealed class SelfCastEffect : MonoBehaviour
    {
        [SerializeField] private float _duration = 1f;
        [SerializeField] private Color _effectColor = new Color(0.3f, 1f, 0.4f, 0.5f);

        private float _elapsed;

        public void Play()
        {
            _elapsed = 0f;

            var renderer = GetComponentInChildren<Renderer>();
            if (renderer != null)
                renderer.material.color = _effectColor;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;

            var scale = Mathf.Lerp(1f, 1.5f, _elapsed / _duration);
            transform.localScale = Vector3.one * scale;

            if (_elapsed >= _duration)
                Destroy(gameObject);
        }
    }
}
