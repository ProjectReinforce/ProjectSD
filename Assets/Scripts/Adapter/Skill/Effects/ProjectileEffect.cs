using UnityEngine;
using SwDreams.Adapter.Manager;

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
            // Skill은 Player의 자식이므로 root가 Player
            playerTransform = transform.root;
            playerStats = playerTransform.GetComponent<PlayerStats>();

            if (projectilePrefab != null)
                PoolManager.Instance?.Prewarm(projectilePrefab, 20);
        }

        public override void Execute(Skill skill)
        {
            if (projectilePrefab == null || playerTransform == null) return;

            Vector2 direction = GetAimDirection();

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
                SpawnProjectile(skill, direction, speed);
            }
            else
            {
                float spreadAngle = 15f;
                float startAngle = -(count - 1) * spreadAngle * 0.5f;

                for (int i = 0; i < count; i++)
                {
                    Vector2 dir = RotateVector(direction, startAngle + i * spreadAngle);
                    SpawnProjectile(skill, dir, speed);
                }
            }
        }

        /// <summary>
        /// SkillManager에서 동적 생성 시 프리팹 설정용.
        /// </summary>
        public void SetProjectilePrefab(GameObject prefab)
        {
            projectilePrefab = prefab;

            if (prefab != null)
                PoolManager.Instance?.Prewarm(prefab, 20);
        }

        private void SpawnProjectile(Skill skill, Vector2 direction, float speed)
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

            projectile.Initialize(
                position: (Vector2)playerTransform.position,
                direction: direction,
                damage: damage,
                speed: speed,
                lifetime: skill.Data.projectileLifetime
            );
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
