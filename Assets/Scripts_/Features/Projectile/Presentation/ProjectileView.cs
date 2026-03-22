using UnityEngine;

namespace Features.Projectile.Presentation
{
    public sealed class ProjectileView : MonoBehaviour
    {
        [SerializeField] private TrailRenderer _trail;
        [SerializeField] private float _lifetime = 6f;

        private void Start()
        {
            Destroy(gameObject, _lifetime);
        }

        public void SetColor(Color color)
        {
            var renderer = GetComponentInChildren<Renderer>();
            if (renderer != null)
                renderer.material.color = color;

            if (_trail != null)
            {
                _trail.startColor = color;
                _trail.endColor = new Color(color.r, color.g, color.b, 0f);
            }
        }
    }
}
