using UnityEngine;
using Photon.Pun;
using SwDreams.Domain.Interfaces;
using SwDreams.Adapter.Manager;
using SwDreams.Adapter.Entity;

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

                    var enemy = hit.GetComponent<Enemy>();
                    if (enemy != null && enemy.IsAlive)
                    {
                        enemy.TakeDamage(damage / 2); // 폭발 데미지 = 직격의 50%

                        if (knockbackForce > 0f)
                            enemy.ApplyKnockback(transform.position, knockbackForce * 0.7f); // 폭발 넉백은 70%
                    }
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