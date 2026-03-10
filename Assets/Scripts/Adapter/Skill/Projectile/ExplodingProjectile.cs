using UnityEngine;
using Photon.Pun;
using SwDreams.Domain.Interfaces;
using SwDreams.Adapter.Manager;

namespace SwDreams.Adapter.Skill
{
    /// <summary>
    /// 폭발 투사체. 폭렬 표창 진화용.
    /// 적 적중 시 OverlapCircle로 범위 폭발 데미지.
    /// </summary>
    public class ExplodingProjectile : Projectile
    {
        private float explosionRadius = 1.5f;

        public void SetExplosion(float radius)
        {
            explosionRadius = radius;
        }

        protected override void OnHitEnemy(Collider2D other)
        {
            // 호스트: 범위 폭발
            if (PhotonNetwork.IsMasterClient)
            {
                var hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
                foreach (var hit in hits)
                {
                    if (!hit.CompareTag("Enemy")) continue;
                    if (hit == other) continue; // 직접 맞은 적은 base에서 이미 처리

                    var damageable = hit.GetComponent<IDamageable>();
                    if (damageable != null && damageable.IsAlive)
                        damageable.TakeDamage(damage / 2); // 폭발 데미지 = 직격의 50%
                }
            }

            // TODO: 폭발 비주얼 이펙트 (PoolManager.Get)

            ReturnToPool();
        }

        public override void OnSpawnFromPool()
        {
            base.OnSpawnFromPool();
        }
    }
}
