using UnityEngine;
using SwDreams.Features.Skill.Domain.ValueObjects;
using SwDreams.Features.Skill.Adapter.Data;
using SwDreams.Features.Skill.Adapter;
using SwDreams.Shared.Managers;
using SwDreams.Features.Skill.Adapter.Spread;
using SwDreams.Features.Skill.Adapter.Trajectories;
using SwDreams.Shared.Data;

namespace SwDreams.Features.Skill.Adapter
{
    /// <summary>
    /// 투사체 스폰 담당. ISkillSpawner 구현.
    ///
    /// 기존 ProjectileEffect.SpawnProjectile() + SpreadPattern 로직을 이전.
    /// Executor가 FiringMode 타이밍에 맞춰 Spawn()을 호출하면,
    /// SpreadPattern으로 방향을 계산하고 투사체 1개를 스폰.
    ///
    /// 스킬: 표창, 매직미사일, 부메랑, 회오리바람, 두더지, 톱날, 분기탄 등
    ///
    /// [Phase 7 리팩토링] Step 4-3
    /// </summary>
    public class ProjectileSpawner : ISkillSpawner
    {
        private readonly GameObject projectilePrefab;

        public ProjectileSpawner(GameObject prefab)
        {
            projectilePrefab = prefab;
        }

        public void Prewarm(SkillData data)
        {
            if (projectilePrefab != null)
                PoolManager.Instance?.Prewarm(projectilePrefab, 20);
        }

        public void Cleanup()
        {
            // 투사체는 독립 생명주기 — 정리 불필요
        }

        public void Spawn(SpawnContext ctx)
        {
            if (projectilePrefab == null) return;

            SkillData data = ctx.skillData;

            // ── SpreadPattern으로 이 투사체의 방향 결정 ──
            Vector2 direction = ComputeDirection(ctx);

            // ── 풀에서 투사체 가져오기 ──
            GameObject obj = PoolManager.Instance.Get(projectilePrefab);
            var projectile = obj.GetComponent<Projectile>();

            if (projectile == null)
            {
                Debug.LogError("[ProjectileSpawner] Projectile 컴포넌트 없음");
                PoolManager.Instance.Return(obj);
                return;
            }

            // ── 초기화 (SpawnContext에서 필터링 완료된 값 사용) ──
            projectile.Initialize(
                position: ctx.playerPosition,
                direction: direction,
                damage: ctx.damage,
                speed: ctx.projectileSpeed,
                lifetime: data.projectileLifetime,
                knockbackForce: ctx.knockbackForce
            );

            // ── TriggerSystem + 소유자 연결 (항상 호출 — 소유자 판별에 필요) ──
            projectile.SetTriggerSystem(ctx.triggerSystem, ctx.playerTransform);

            // ── 치명타 파라미터 (R9) ──
            projectile.SetCritStats(ctx.critChance, ctx.critDamageMultiplier);

            // ── Trajectory 부착 (R6/B1: pullRadius 에 ctx.skillRangeBonus 반영) ──
            ITrajectoryBehavior trajectory = TrajectoryFactory.Create(
                data.trajectoryType, data, ctx.skillRangeBonus);

            if (trajectory is SpiralTrajectory spiral)
            {
                spiral.SetOrigin(ctx.playerPosition);
                if (ctx.totalCount > 1)
                {
                    float startAngle = (360f / ctx.totalCount) * ctx.fireIndex;
                    trajectory = new SpiralTrajectory(
                        data.pullRadius + ctx.skillRangeBonus, data.pullForce,
                        data.spiralExpandSpeed, startAngle);
                    ((SpiralTrajectory)trajectory).SetOrigin(ctx.playerPosition);
                }
            }

            projectile.SetTrajectory(trajectory);

            // ── 관통 설정 ──
            if (data.penetrates)
                projectile.SetPenetrates(true);

            // ── 체인 비행 설정 ──
            if (data.chainFlightCount > 0)
                projectile.SetChainFlight(data.chainFlightCount, data.chainSearchRadius);

            // ── 서브 투사체 프리팹 (분기탄 등) ──
            if (data.subProjectilePrefab != null)
                projectile.SetSubProjectilePrefab(data.subProjectilePrefab);
        }

        /// <summary>
        /// SpreadPattern을 사용해 fireIndex에 해당하는 방향 계산.
        /// SimultaneousSpread: 같은 baseDirection으로 n개 방향 중 하나 선택.
        /// DelayedBurst: 매 호출마다 baseDirection이 재계산되므로 항상 index 0.
        /// Single: totalCount=1이므로 baseDirection 그대로.
        /// </summary>
        private Vector2 ComputeDirection(SpawnContext ctx)
        {
            SkillData data = ctx.skillData;
            Vector2 baseDir = ctx.baseDirection;

            if (baseDir.sqrMagnitude < 0.01f)
                baseDir = Vector2.right;

            // Single/DelayedBurst에서 totalCount=1이거나 매번 방향 재계산이면
            // SpreadPattern 적용 시 index=0으로 단일 방향 사용
            int spreadCount = ctx.totalCount;
            int spreadIndex = ctx.fireIndex;

            // DelayedBurst: 매 발사마다 방향이 재계산되므로 SpreadPattern은 1발 기준
            if (data.firingMode == FiringMode.DelayedBurst)
            {
                spreadCount = 1;
                spreadIndex = 0;
            }

            ISpreadPattern spread = SpreadPatternFactory.Create(data.spreadPattern, data.spreadAngle);
            Vector2[] directions = spread.GetDirections(baseDir, spreadCount);

            if (spreadIndex >= 0 && spreadIndex < directions.Length)
                return directions[spreadIndex];

            return baseDir;
        }
    }
}