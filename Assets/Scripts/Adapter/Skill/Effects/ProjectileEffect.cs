using UnityEngine;
using SwDreams.Adapter.Manager;
using SwDreams.Adapter.Skill.TriggerEffects;
using SwDreams.Adapter.Skill.Spread;
using SwDreams.Adapter.Skill.Trajectories;
using SwDreams.Domain.ValueObjects;
using SwDreams.Data;

namespace SwDreams.Adapter.Skill
{
    /// <summary>
    /// 투사체 기반 스킬 효과.
    ///
    /// [Step 3-7d] SpreadPattern + TrajectoryBehavior 조합 모델로 전환.
    /// - SpreadPattern: 다중 투사체 배치 (Fan, Radial, Single, Random)
    /// - TrajectoryBehavior: 궤적 (Straight, Homing, Boomerang, Tornado 등)
    /// - TriggerEffect: 적중/소멸 효과 (Explode, Chain 등) — SO triggerEffects에서 정의
    ///
    /// 투사체는 로컬 전용 (네트워크 동기화 없음).
    /// </summary>
    public class ProjectileEffect : SkillEffect
    {
        [SerializeField] private GameObject projectilePrefab;

        private Transform playerTransform;
        private PlayerStats playerStats;
        private SkillTriggerSystem triggerSystem;

        private void Start()
        {
            CachePlayerReferences();

            if (projectilePrefab != null)
                PoolManager.Instance?.Prewarm(projectilePrefab, 20);
        }

        private void CachePlayerReferences()
        {
            if (playerTransform == null)
            {
                playerTransform = transform.root;
                if (playerTransform != null)
                    playerStats = playerTransform.GetComponent<PlayerStats>();
            }
            // triggerSystem은 SkillTriggerSystem이 나중에 붙을 수 있으므로 매번 체크
            if (triggerSystem == null)
            {
                triggerSystem = GetComponent<SkillTriggerSystem>();
                if (triggerSystem != null)
                    Debug.Log($"[ProjectileEffect] TriggerSystem 발견! 효과 수: {triggerSystem.TotalEffectCount}");
            }
        }

        public override void Execute(Skill skill)
        {
            CachePlayerReferences();
            if (projectilePrefab == null || playerTransform == null) return;

            SkillData data = skill.Data;
            Vector2 baseDirection = GetBaseDirection(data.aimType);

            if (baseDirection.sqrMagnitude < 0.01f)
                baseDirection = Vector2.right;

            // PlayerStats 보너스 적용
            int count = data.projectileCount;
            float speed = data.projectileSpeed;
            if (playerStats != null)
            {
                count = playerStats.GetEffectiveProjectileCount(count);
                speed = playerStats.GetEffectiveProjectileSpeed(speed);
            }

            // SpreadPattern으로 방향 배열 생성
            ISpreadPattern spread = SpreadPatternFactory.Create(data.spreadPattern, data.spreadAngle);
            Vector2[] directions = spread.GetDirections(baseDirection, count);

            // 각 투사체 스폰
            for (int i = 0; i < directions.Length; i++)
                SpawnProjectile(skill, directions[i], speed, i, directions.Length);

            // OnFire 트리거
            if (triggerSystem != null && triggerSystem.HasTrigger(TriggerType.OnFire))
            {
                triggerSystem.FireTrigger(TriggerType.OnFire, new TriggerContext
                {
                    position = playerTransform.position,
                    direction = baseDirection,
                    owner = playerTransform
                });
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

            SkillData data = skill.Data;

            // 데미지 계산
            int damage = skill.CurrentDamage;
            if (playerStats != null)
                damage = Mathf.RoundToInt(damage * playerStats.AttackMultiplier);

            // 넉백
            float knockback = 0f;
            var cfg = GameManager.Instance?.Config;
            if (cfg != null)
                knockback = cfg.baseKnockbackForce;
            if (playerStats != null)
                knockback *= playerStats.KnockbackMultiplier;

            // 초기화
            projectile.Initialize(
                position: (Vector2)playerTransform.position,
                direction: direction,
                damage: damage,
                speed: speed,
                lifetime: data.projectileLifetime,
                knockbackForce: knockback
            );

            // TriggerSystem 연결 (triggerEffects가 있는 스킬만 보유)
            if (triggerSystem != null)
                projectile.SetTriggerSystem(triggerSystem, playerTransform);

            // Trajectory 부착
            ITrajectoryBehavior trajectory = TrajectoryFactory.Create(data.trajectoryType, data);

            // 나선형: 원점 + 시작 각도 설정
            if (trajectory is SpiralTrajectory spiral)
            {
                spiral.SetOrigin(playerTransform.position);
                // 다중 투사체일 때 시작 각도 분배
                if (totalCount > 1)
                {
                    float startAngle = (360f / totalCount) * index;
                    // SpiralTrajectory의 startAngle은 생성자에서 설정되므로 새로 생성
                    trajectory = new SpiralTrajectory(
                        data.pullRadius, data.pullForce,
                        data.spiralExpandSpeed, startAngle);
                    ((SpiralTrajectory)trajectory).SetOrigin(playerTransform.position);
                }
            }

            projectile.SetTrajectory(trajectory);

            // SO에서 관통 설정
            if (data.penetrates)
                projectile.SetPenetrates(true);
        }

        // ===== 발사 방향 =====

        private Vector2 lastMoveDirection = Vector2.right;

        /// <summary>
        /// AimType에 따라 기준 방향을 결정.
        /// SpreadPattern 적용 전 base direction.
        /// </summary>
        private Vector2 GetBaseDirection(AimType aimType)
        {
            Rigidbody2D rb;

            switch (aimType)
            {
                case AimType.ClosestEnemy:
                    Transform closest = FindClosestEnemy();
                    if (closest != null)
                        return ((Vector2)(closest.position - playerTransform.position)).normalized;
                    return lastMoveDirection;

                case AimType.MoveDirection:
                    rb = playerTransform.GetComponent<Rigidbody2D>();
                    if (rb != null && rb.linearVelocity.sqrMagnitude > 0.1f)
                    {
                        lastMoveDirection = rb.linearVelocity.normalized;
                        return lastMoveDirection;
                    }
                    return lastMoveDirection;

                case AimType.ReverseMoveDirection:
                    rb = playerTransform.GetComponent<Rigidbody2D>();
                    if (rb != null && rb.linearVelocity.sqrMagnitude > 0.1f)
                    {
                        lastMoveDirection = rb.linearVelocity.normalized;
                        return -lastMoveDirection;
                    }
                    return -lastMoveDirection;

                case AimType.Random:
                    float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
                    return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                default:
                    return Vector2.right;
            }
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
    }
}