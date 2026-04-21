using UnityEngine;
using Photon.Pun;
using SwDreams.Features.Character.Adapter;
using SwDreams.Shared.Domain.Interfaces;
using SwDreams.Shared.Managers;

namespace SwDreams.Features.Enemy.Adapter.Attack
{
    /// <summary>
    /// 원거리 적의 직선 투사체.
    /// - 모든 클라이언트에서 로컬 이동·렌더
    /// - 히트 판정은 호스트만
    /// - Skill.Projectile 과 독립 (관통·체인·Trajectory 미지원)
    ///
    /// 수명 만료 또는 플레이어 피격 시 풀에 반환.
    /// GameState.Playing/BossFight 외에는 정지 (일시정지 대응).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class EnemyProjectile : MonoBehaviour, IPoolable
    {
        private Vector2 direction;
        private float speed;
        private int damage;
        private float lifetime;
        private float aliveTime;
        private bool isActive;

        public void Initialize(Vector2 pos, Vector2 dir, float speed, int damage, float lifetime)
        {
            transform.position = pos;
            this.direction = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
            this.speed = speed;
            this.damage = damage;
            this.lifetime = lifetime;
            this.aliveTime = 0f;
            this.isActive = true;

            float angle = Mathf.Atan2(this.direction.y, this.direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void Update()
        {
            if (!isActive) return;

            // 씬 전환 중(GameManager 파괴)엔 안전하게 정지
            if (GameManager.Instance == null) return;
            var state = GameManager.Instance.CurrentState;
            if (state != GameManager.GameState.Playing &&
                state != GameManager.GameState.BossFight)
                return;

            transform.position += (Vector3)(direction * speed * Time.deltaTime);

            aliveTime += Time.deltaTime;
            if (aliveTime >= lifetime)
                ReturnToPool();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!isActive) return;
            if (!other.CompareTag("Player")) return;

            // 데미지 판정은 호스트만. 클라이언트는 충돌 시 비주얼만 소멸.
            if (PhotonNetwork.IsMasterClient)
            {
                var player = other.GetComponent<PlayerStub>();
                if (player != null && player.IsAlive)
                    player.TakeDamage(damage);
            }

            ReturnToPool();
        }

        private void ReturnToPool()
        {
            isActive = false;
            if (PoolManager.Instance != null)
                PoolManager.Instance.Return(gameObject);
            else
                gameObject.SetActive(false);
        }

        public void OnSpawnFromPool()
        {
            gameObject.SetActive(true);
        }

        public void OnReturnToPool()
        {
            isActive = false;
            gameObject.SetActive(false);
        }
    }
}
