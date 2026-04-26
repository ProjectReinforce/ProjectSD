using System.Collections.Generic;
using SwDreams.Features.Skill.Adapter.Data;
using SwDreams.Features.Skill.Adapter;
using UnityEngine;
using SwDreams.Shared.Managers;
using SwDreams.Shared.Data;

namespace SwDreams.Features.Skill.Adapter
{
    /// <summary>
    /// 설치형 오브젝트 스폰 담당. ISkillSpawner 구현.
    ///
    /// 기존 PlacedEffect의 포탑 생성 + activeTurrets 관리 로직 이전.
    /// maxInstances 초과 시 가장 오래된 오브젝트 제거.
    ///
    /// 스킬: 자동포탑(DelayedBurst), 출입제한구역 표지판(Single) 등
    ///
    /// [Phase 7 리팩토링] Step 4-5
    /// </summary>
    public class PlacedSpawner : ISkillSpawner
    {
        private readonly GameObject turretPrefab;
        private readonly int maxInstances;
        private readonly List<GameObject> activeTurrets = new List<GameObject>();

        public PlacedSpawner(GameObject prefab, int maxInstances)
        {
            this.turretPrefab = prefab;
            this.maxInstances = maxInstances;
        }

        public void Prewarm(SkillData data)
        {
            if (turretPrefab != null)
                PoolManager.Instance?.Prewarm(turretPrefab, maxInstances + 2);
        }

        public void Cleanup()
        {
            // 포탑은 자체 duration으로 소멸
        }

        public void Spawn(SpawnContext ctx)
        {
            if (turretPrefab == null) return;

            SkillData data = ctx.skillData;

            // 비활성 포탑 정리
            CleanupDestroyedTurrets();

            // 최대 개수 초과 시 가장 오래된 포탑 제거
            while (activeTurrets.Count >= maxInstances)
                RemoveOldestTurret();

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

            activeTurrets.Add(turretObj);
        }

        private void CleanupDestroyedTurrets()
        {
            for (int i = activeTurrets.Count - 1; i >= 0; i--)
            {
                if (activeTurrets[i] == null || !activeTurrets[i].activeInHierarchy)
                    activeTurrets.RemoveAt(i);
            }
        }

        private void RemoveOldestTurret()
        {
            if (activeTurrets.Count == 0) return;

            GameObject oldest = activeTurrets[0];
            activeTurrets.RemoveAt(0);

            if (oldest != null && oldest.activeInHierarchy)
                PoolManager.Instance?.Return(oldest);
        }
    }
}