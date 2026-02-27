using UnityEngine;
using Photon.Pun;
using SwDreams.Domain.Interfaces;
using SwDreams.Adapter.Manager;

namespace SwDreams.Adapter.Skill
{
    /// <summary>
    /// 투사체 엔티티.
    /// 모든 클라이언트에서 로컬로 이동 + 렌더링.
    /// 히트 판정은 호스트에서만 처리.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Projectile : MonoBehaviour, IPoolable
    {
        private Vector2 direction;
        private float speed;
        private int damage;
        private float lifetime;
        private float aliveTime;

        public void Initialize(Vector2 position, Vector2 direction,
            int damage, float speed, float lifetime)
        {
            transform.position = position;
            this.direction = direction.normalized;
            this.damage = damage;
            this.speed = speed;
            this.lifetime = lifetime;
            aliveTime = 0f;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }

        private void Update()
        {
            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentState != GameManager.GameState.Playing)
                return;
                
            transform.position += (Vector3)(direction * speed * Time.deltaTime);

            aliveTime += Time.deltaTime;
            if (aliveTime >= lifetime)
                ReturnToPool();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Enemy")) return;

            // 호스트에서만 데미지 적용
            if (PhotonNetwork.IsMasterClient)
            {
                var damageable = other.GetComponent<IDamageable>();
                if (damageable != null && damageable.IsAlive)
                {
                    damageable.TakeDamage(damage);
                }
            }

            ReturnToPool();
        }

        private void ReturnToPool()
        {
            PoolManager.Instance?.Return(gameObject);
        }

        public void OnSpawnFromPool()
        {
            gameObject.SetActive(true);
            aliveTime = 0f;
        }

        public void OnReturnToPool()
        {
            gameObject.SetActive(false);
        }
    }
}
