using UnityEngine;
using Photon.Pun;
using SwDreams.Domain.Interfaces;
using SwDreams.Adapter.Manager;
using SwDreams.Adapter.Entity;

namespace SwDreams.Adapter.Skill
{
    /// <summary>
    /// 체인 투사체. 체인 미사일 진화용.
    /// 적 적중 후 근처 적에게 유도 이동 (체인). 체인 횟수만큼 반복.
    /// HomingProjectile 기반이지만 히트 시 새 타겟으로 재유도.
    /// </summary>
    public class ChainProjectile : Projectile
    {
        private int remainingChains;
        private float chainRadius = 4f;
        private float rotateSpeed = 400f;
        private Transform currentTarget;
        private float retargetTimer;
        private const float RETARGET_INTERVAL = 0.1f;

        // 이미 맞은 적 추적 (같은 적에게 반복 체인 방지)
        private System.Collections.Generic.HashSet<int> hitEnemyIds
            = new System.Collections.Generic.HashSet<int>();

        public void SetChain(int chainCount, float chainRadius, float rotateSpeed)
        {
            remainingChains = chainCount;
            this.chainRadius = chainRadius;
            this.rotateSpeed = rotateSpeed;
            hitEnemyIds.Clear();
            FindTarget();
        }

        protected override void MoveStep()
        {
            // 타겟 재탐색
            retargetTimer += Time.deltaTime;
            if (retargetTimer >= RETARGET_INTERVAL)
            {
                retargetTimer = 0f;
                if (currentTarget == null || !currentTarget.gameObject.activeInHierarchy)
                    FindTarget();
            }

            // 타겟 유도
            if (currentTarget != null && currentTarget.gameObject.activeInHierarchy)
            {
                Vector2 toTarget = ((Vector2)currentTarget.position - (Vector2)transform.position).normalized;
                float maxAngle = rotateSpeed * Time.deltaTime;
                direction = RotateTowards(direction, toTarget, maxAngle);
            }

            transform.position += (Vector3)(direction * speed * Time.deltaTime);
            UpdateRotation(direction);
        }

        /// <summary>
        /// 이미 맞은 적은 스킵 (체인 이동 중 재접촉 방지).
        /// </summary>
        protected override void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Enemy")) return;
            if (hitEnemyIds.Contains(other.gameObject.GetInstanceID())) return;

            // 호스트에서만 데미지 + 넉백
            if (PhotonNetwork.IsMasterClient)
            {
                var damageable = other.GetComponent<IDamageable>();
                if (damageable != null && damageable.IsAlive)
                {
                    damageable.TakeDamage(damage);

                    if (knockbackForce > 0f)
                    {
                        var enemy = other.GetComponent<Enemy>();
                        if (enemy != null)
                            enemy.ApplyKnockback(transform.position, knockbackForce);
                    }
                }
            }

            OnHitEnemy(other);
        }

        protected override void OnHitEnemy(Collider2D other)
        {
            // 히트한 적 기록
            int instanceId = other.gameObject.GetInstanceID();
            hitEnemyIds.Add(instanceId);

            if (remainingChains > 0)
            {
                remainingChains--;
                aliveTime = 0f; // 체인 시 lifetime 리셋 (체인 도중 소멸 방지)

                // 다음 체인 타겟 탐색
                Transform nextTarget = FindNextChainTarget(other.transform.position);
                if (nextTarget != null)
                {
                    currentTarget = nextTarget;
                    direction = ((Vector2)nextTarget.position - (Vector2)transform.position).normalized;
                    // 풀 반환 안 함 — 다음 타겟으로 계속 이동
                    return;
                }
            }

            // 체인 소진 or 타겟 없음 → 소멸
            ReturnToPool();
        }

        private void FindTarget()
        {
            currentTarget = null;
            float minDist = float.MaxValue;

            var enemies = GameObject.FindGameObjectsWithTag("Enemy");
            foreach (var e in enemies)
            {
                if (!e.activeInHierarchy) continue;
                if (hitEnemyIds.Contains(e.GetInstanceID())) continue;

                float dist = Vector2.Distance(transform.position, e.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    currentTarget = e.transform;
                }
            }
        }

        private Transform FindNextChainTarget(Vector3 fromPosition)
        {
            Transform closest = null;
            float minDist = chainRadius;

            var enemies = GameObject.FindGameObjectsWithTag("Enemy");
            foreach (var e in enemies)
            {
                if (!e.activeInHierarchy) continue;
                if (hitEnemyIds.Contains(e.GetInstanceID())) continue;

                float dist = Vector2.Distance(fromPosition, e.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = e.transform;
                }
            }

            return closest;
        }

        private Vector2 RotateTowards(Vector2 from, Vector2 to, float maxDegrees)
        {
            float fromAngle = Mathf.Atan2(from.y, from.x) * Mathf.Rad2Deg;
            float toAngle = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
            float newAngle = Mathf.MoveTowardsAngle(fromAngle, toAngle, maxDegrees);
            float rad = newAngle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }

        public override void OnSpawnFromPool()
        {
            base.OnSpawnFromPool();
            currentTarget = null;
            retargetTimer = 0f;
            remainingChains = 0;
            hitEnemyIds.Clear();
        }
    }
}