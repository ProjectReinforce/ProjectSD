using System.Collections.Generic;
using UnityEngine;
using SwDreams.Adapter.Manager;
using SwDreams.Data;

namespace SwDreams.Adapter.Skill
{
    /// <summary>
    /// 장판(지대) 스폰 담당. ISkillSpawner 구현.
    ///
    /// 기존 AreaEffect의 장판 생성 + activeZones 관리 로직을 이전.
    /// Executor가 Spawn()을 호출하면 장판 1개를 생성.
    /// maxInstances 초과 시 가장 오래된 장판을 제거.
    ///
    /// 스킬: 번개(DelayedBurst), 개미지옥(DelayedBurst), 성역(Single),
    ///       장풍, 별똥별, 뇌전역(진화) 등
    ///
    /// [Phase 7 리팩토링] Step 4-4
    /// </summary>
    public class AreaSpawner : ISkillSpawner
    {
        private readonly GameObject zonePrefab;
        private readonly int maxInstances;

        // 활성 장판 추적 (maxInstances 관리). 상태 유지 필요.
        private readonly List<GameObject> activeZones = new List<GameObject>();

        public AreaSpawner(GameObject prefab, int maxInstances)
        {
            this.zonePrefab = prefab;
            this.maxInstances = maxInstances;
        }

        public void Prewarm(SkillData data)
        {
            if (zonePrefab != null)
                PoolManager.Instance?.Prewarm(zonePrefab, maxInstances + 2);
        }

        public void Cleanup()
        {
            // 장판은 자체 duration으로 소멸 — 강제 정리 불필요
        }

        public void Spawn(SpawnContext ctx)
        {
            if (zonePrefab == null) return;

            SkillData data = ctx.skillData;

            // 오래된 장판 정리
            CleanupDestroyedZones();

            // 최대 개수 초과 시 가장 오래된 장판 제거
            while (activeZones.Count >= maxInstances)
                RemoveOldestZone();

            // 풀에서 장판 가져오기
            GameObject zoneObj = PoolManager.Instance.Get(zonePrefab);
            var zone = zoneObj.GetComponent<AreaZone>();

            if (zone == null)
            {
                Debug.LogError("[AreaSpawner] AreaZone 컴포넌트 없음");
                PoolManager.Instance.Return(zoneObj);
                return;
            }

            // ── 스탯 계산 (SpawnContext에서 필터링 완료된 값 사용) ──
            float radius = data.areaRadius + ctx.skillRangeBonus;
            float duration = data.areaDuration + ctx.skillDurationBonus;

            // 데미지: 회복 장판이면 rawDamage * healMultiplier, 피해 장판이면 damage(공격력 적용 완료)
            int damage;
            if (data.isHealingEffect)
                damage = Mathf.RoundToInt(ctx.rawDamage * ctx.healMultiplier);
            else
                damage = ctx.damage;

            // ── 스폰 위치 결정 ──
            Vector2 spawnPos;
            if (data.spawnAtRandomPosition)
            {
                // 번개/개미지옥: 플레이어 주변 랜덤 위치
                float spawnRadius = data.randomSpawnRadius + ctx.skillRangeBonus;
                Vector2 randomOffset = Random.insideUnitCircle * spawnRadius;
                spawnPos = ctx.playerPosition + randomOffset;
            }
            else
            {
                // 성역: 플레이어 위치
                spawnPos = ctx.playerPosition;
            }

            // ── 장판 초기화 ──
            zone.Initialize(
                position: spawnPos,
                damage: damage,
                radius: radius,
                duration: duration,
                tickRate: data.tickRate,
                isHealing: data.isHealingEffect
            );

            // TriggerSystem + 소유자 연결 (항상 호출 — 소유자 판별에 필요)
            zone.SetTriggerSystem(ctx.triggerSystem, ctx.playerTransform);

            activeZones.Add(zoneObj);
        }

        // ===== activeZones 관리 =====

        private void CleanupDestroyedZones()
        {
            for (int i = activeZones.Count - 1; i >= 0; i--)
            {
                if (activeZones[i] == null || !activeZones[i].activeInHierarchy)
                    activeZones.RemoveAt(i);
            }
        }

        private void RemoveOldestZone()
        {
            if (activeZones.Count == 0) return;

            GameObject oldest = activeZones[0];
            activeZones.RemoveAt(0);

            if (oldest != null && oldest.activeInHierarchy)
                PoolManager.Instance?.Return(oldest);
        }
    }
}