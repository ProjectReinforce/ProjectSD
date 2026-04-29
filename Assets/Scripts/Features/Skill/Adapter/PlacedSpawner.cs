using SwDreams.Features.Skill.Adapter.Data;
using SwDreams.Features.Skill.Adapter;
using UnityEngine;
using SwDreams.Shared.Managers;
using SwDreams.Shared.Data;

namespace SwDreams.Features.Skill.Adapter
{
    /// <summary>
    /// 설치형 오브젝트 스폰 담당. ISkillSpawner 구현.
    /// 포탑/마커는 자체 duration 으로 자연 소멸.
    ///
    /// maxInstances 인공 한도는 제거됨 (2026-04-29) — 자연 한도(duration / cooldown 비율) +
    /// PoolManager 동적 확장으로 충분. 자세한 정책은 [AreaSpawner] 주석 참조.
    ///
    /// 스킬: 자동포탑(DelayedBurst), 출입제한구역 표지판(Single) 등
    /// </summary>
    public class PlacedSpawner : ISkillSpawner
    {
        private const int InitialPrewarmCount = 8;

        private readonly GameObject turretPrefab;

        public PlacedSpawner(GameObject prefab)
        {
            this.turretPrefab = prefab;
        }

        public void Prewarm(SkillData data)
        {
            if (turretPrefab != null)
                PoolManager.Instance?.Prewarm(turretPrefab, InitialPrewarmCount);
        }

        public void Cleanup()
        {
            // 포탑은 자체 duration으로 소멸
        }

        public void Spawn(SpawnContext ctx)
        {
            if (turretPrefab == null) return;

            SkillData data = ctx.skillData;

            // 풀에서 포탑 가져오기
            GameObject turretObj = PoolManager.Instance.Get(turretPrefab);
            var turret = turretObj.GetComponent<PlacedTurret>();

            if (turret == null)
            {
                Debug.LogError("[PlacedSpawner] PlacedTurret 컴포넌트 없음");
                PoolManager.Instance.Return(turretObj);
                return;
            }

            // ── 스탯 계산 (SpawnContext에서 필터링 완료된 값 사용) ──
            float range = data.attackRange + ctx.skillRangeBonus;
            float duration = data.areaDuration + ctx.skillDurationBonus;

            turret.Initialize(
                position: ctx.playerPosition,
                damage: ctx.damage,
                attackRange: range,
                attackCooldown: data.attackCooldown,
                duration: duration,
                alwaysCritical: data.alwaysCritical,
                critChance: ctx.critChance,
                critDamageMultiplier: ctx.critDamageMultiplier,
                ownerTransform: ctx.playerTransform,
                triggerSystem: ctx.triggerSystem
            );
        }
    }
}