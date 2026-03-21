using UnityEngine;
using SwDreams.Adapter.Manager;
using SwDreams.Data;

namespace SwDreams.Adapter.Skill
{
    /// <summary>
    /// 투사체 기반 스킬 효과.
    /// Phase 2: 직선 투사체 (표창).
    /// Phase 5: 유도(매직미사일), 왕복(부메랑), CC(회오리바람).
    /// 
    /// 투사체는 로컬 전용 (네트워크 동기화 없음).
    /// </summary>
    public class ProjectileEffect : SkillEffect
    {
        [SerializeField] private GameObject projectilePrefab;

        private Transform playerTransform;
        private PlayerStats playerStats;

        private void Start()
        {
            CachePlayerReferences();

            if (projectilePrefab != null)
                PoolManager.Instance?.Prewarm(projectilePrefab, 20);
        }

        private void CachePlayerReferences()
        {
            if (playerTransform != null) return;
            // Skill은 Player의 자식이므로 root가 Player
            playerTransform = transform.root;
            if (playerTransform != null)
                playerStats = playerTransform.GetComponent<PlayerStats>();
        }

        public override void Execute(Skill skill)
        {
            CachePlayerReferences();
            if (projectilePrefab == null || playerTransform == null) return;

            Vector2 direction = GetAimDirection();

            // [Phase 5] 회오리바람: 플레이어 이동 반대 방향으로 발사
            if (skill.Data.isTornado)
            {
                var rb = playerTransform.GetComponent<Rigidbody2D>();
                if (rb != null && rb.linearVelocity.sqrMagnitude > 0.1f)
                    direction = -rb.linearVelocity.normalized;
                else
                    direction = -direction;
            }

            // 방향이 zero면 기본값 (적과 완전 겹칠 때 등)
            if (direction.sqrMagnitude < 0.01f)
                direction = Vector2.right;

            // PlayerStats 보너스 적용
            int count = skill.Data.projectileCount;
            float speed = skill.Data.projectileSpeed;

            if (playerStats != null)
            {
                count = playerStats.GetEffectiveProjectileCount(count);
                speed = playerStats.GetEffectiveProjectileSpeed(speed);
            }

            if (count <= 1)
            {
                SpawnProjectile(skill, direction, speed, 0, 1);
            }
            else if (skill.Data.isSpiral)
            {
                // 나선형: 360도 균등 분배 (장검처럼)
                float angleStep = 360f / count;
                for (int i = 0; i < count; i++)
                    SpawnProjectile(skill, direction, speed, i, count);
            }
            else
            {
                float spreadAngle = 15f;
                float startAngle = -(count - 1) * spreadAngle * 0.5f;

                for (int i = 0; i < count; i++)
                {
                    Vector2 dir = RotateVector(direction, startAngle + i * spreadAngle);
                    SpawnProjectile(skill, dir, speed, i, count);
                }
            }
        }

        /// <summary>
        /// SkillManager에서 동적 생성 시 프리팹 설정용.
        /// </summary>
        public void SetProjectilePrefab(GameObject prefab)
        {
            projectilePrefab = prefab;
            CachePlayerReferences();

            if (prefab != null)
                PoolManager.Instance?.Prewarm(prefab, 20);
        }

        private void SpawnProjectile(Skill skill, Vector2 direction, float speed,
            int index = 0, int totalCount = 1)
        {
            GameObject obj = PoolManager.Instance.Get(projectilePrefab);
            var projectile = obj.GetComponent<Projectile>();

            if (projectile == null)
            {
                Debug.LogError("[ProjectileEffect] Projectile 컴포넌트 없음");
                PoolManager.Instance.Return(obj);
                return;
            }

            int damage = skill.CurrentDamage;
            if (playerStats != null)
                damage = Mathf.RoundToInt(damage * playerStats.AttackMultiplier);

            // 넉백 힘: Config 기본값 * PlayerStats 배율
            float knockback = 0f;
            var cfg = GameManager.Instance?.Config;
            if (cfg != null)
                knockback = cfg.baseKnockbackForce;
            if (playerStats != null)
                knockback *= playerStats.KnockbackMultiplier;

            SkillData data = skill.Data;

            projectile.Initialize(
                position: (Vector2)playerTransform.position,
                direction: direction,
                damage: damage,
                speed: speed,
                lifetime: data.projectileLifetime,
                knockbackForce: knockback
            );

            // [Phase 5] 변형 투사체 추가 설정
            if (data.isHoming)
            {
                var homing = projectile as HomingProjectile;
                if (homing != null)
                    homing.SetHoming(data.homingRotateSpeed);
            }
            else if (data.isBoomerang)
            {
                var boomerang = projectile as BoomerangProjectile;
                if (boomerang != null)
                {
                    boomerang.SetBoomerang(playerTransform);

                    // [진화: 그래비톤 부메랑] 복귀 경로 끌어당김
                    if (data.hasPullOnReturn)
                        boomerang.SetPullOnReturn(data.pullRadius, data.pullForce);
                }
            }
            else if (data.isTornado)
            {
                var tornado = projectile as TornadoProjectile;
                if (tornado != null)
                    tornado.SetTornado(data.pullRadius, data.pullForce);
            }

            // [Phase 5 진화] 폭발/체인/나선
            if (data.isExploding)
            {
                var exploding = projectile as ExplodingProjectile;
                if (exploding != null)
                    exploding.SetExplosion(data.explosionRadius);
            }

            if (data.chainCount > 0)
            {
                var chain = projectile as ChainProjectile;
                if (chain != null)
                    chain.SetChain(data.chainCount, data.chainRadius, data.homingRotateSpeed);
            }

            if (data.isSpiral)
            {
                var spiral = projectile as SpiralTornadoProjectile;
                if (spiral != null)
                {
                    float startAngle = (totalCount > 1) ? (360f / totalCount) * index : 0f;
                    spiral.SetSpiral(playerTransform, data.pullRadius, data.pullForce,
                        data.spiralExpandSpeed, startAngle);
                }
            }
        }

        private Vector2 GetAimDirection()
        {
            Transform closest = FindClosestEnemy();
            if (closest != null)
                return ((Vector2)(closest.position - playerTransform.position)).normalized;

            return Vector2.right;
        }

        private Transform FindClosestEnemy()
        {
            var enemies = GameObject.FindGameObjectsWithTag("Enemy");
            if (enemies.Length == 0) return null;

            Transform closest = null;
            float minDist = float.MaxValue;

            foreach (var e in enemies)
            {
                if (!e.activeInHierarchy) continue;
                float dist = Vector2.Distance(playerTransform.position, e.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = e.transform;
                }
            }

            return closest;
        }

        private Vector2 RotateVector(Vector2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }
    }
}