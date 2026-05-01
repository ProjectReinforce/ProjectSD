using SwDreams.Features.Skill.Adapter.Data;
using SwDreams.Features.Skill.Adapter;
using UnityEngine;
using SwDreams.Shared.Managers;
using SwDreams.Shared.Data;

namespace SwDreams.Features.Skill.Adapter
{
    /// <summary>
    /// 장판(지대) 스폰 담당. ISkillSpawner 구현.
    ///
    /// 기존 AreaEffect의 장판 생성 로직 이전.
    /// Executor가 Spawn()을 호출하면 장판 1개를 생성. AreaZone 자체 duration 으로 자연 소멸.
    ///
    /// maxInstances 인공 한도는 제거됨 (2026-04-29) — Survivors-like 화면 도배가 컨셉.
    /// 자연 한도(duration / cooldown 비율) + PoolManager 동적 확장으로 충분.
    /// 극단 케이스(duration 패시브 무한 누적) 보호망이 필요해지면 GameplayConfig 글로벌 한도 도입.
    ///
    /// 스킬: 번개(DelayedBurst), 개미지옥(DelayedBurst), 성역(Single),
    ///       장풍, 별똥별, 뇌전역(진화) 등
    /// </summary>
    public class AreaSpawner : ISkillSpawner
    {
        private const int InitialPrewarmCount = 8;

        private readonly GameObject zonePrefab;

        public AreaSpawner(GameObject prefab)
        {
            this.zonePrefab = prefab;
        }

        public void Prewarm(SkillData data)
        {
            if (zonePrefab != null)
                PoolManager.Instance?.Prewarm(zonePrefab, InitialPrewarmCount);
        }

        public void Cleanup()
        {
            // 장판은 자체 duration으로 소멸 — 강제 정리 불필요
        }

        /// <summary>
        /// [N15] 자기 클라가 발사 결정 시 Random.insideUnitCircle 한 번만 호출.
        /// 결정한 spawnPos 가 RPC 인자로 모든 클라에 전파 → 동일 위치 보장.
        /// </summary>
        public bool TryGenerateSpawnPos(SpawnContext ctx, out Vector2 spawnPos)
        {
            SkillData data = ctx.skillData;
            if (data.spawnAtRandomPosition)
            {
                float spawnRadius = data.randomSpawnRadius + ctx.skillRangeBonus;
                Vector2 randomOffset = Random.insideUnitCircle * spawnRadius;
                spawnPos = ctx.playerPosition + randomOffset;
                return true;
            }
            // 성역 등 비랜덤은 player 위치 그대로 — override 불필요.
            spawnPos = ctx.playerPosition;
            return true;
        }

        public void Spawn(SpawnContext ctx)
        {
            if (zonePrefab == null) return;

            SkillData data = ctx.skillData;

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
            // [N15] hasSpawnPosOverride 가 true 면 자기 클라가 결정한 위치 그대로 사용 (호스트 권위 동기화).
            // false 인 경우는 fallback — 자기 클라가 직접 spawner.Spawn 호출(prediction 미경유) 시.
            Vector2 spawnPos;
            if (ctx.hasSpawnPosOverride)
            {
                spawnPos = ctx.spawnPosOverride;
            }
            else if (data.spawnAtRandomPosition)
            {
                float spawnRadius = data.randomSpawnRadius + ctx.skillRangeBonus;
                Vector2 randomOffset = Random.insideUnitCircle * spawnRadius;
                spawnPos = ctx.playerPosition + randomOffset;
            }
            else
            {
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

            // 치명타 파라미터 (R9). 회복 장판은 critChance=0 으로 (회복 치명타는 별건 결정).
            float critChanceForZone = data.isHealingEffect ? 0f : ctx.critChance;
            zone.SetCritStats(critChanceForZone, ctx.critDamageMultiplier);
        }
    }
}