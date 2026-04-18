using UnityEngine;
using SwDreams.Adapter.Manager;
using SwDreams.Shared.Managers;
using SwDreams.Data;
using SwDreams.Shared.Data;
using SwDreams.Domain.ValueObjects;

namespace SwDreams.Adapter.Skill
{
    /// <summary>
    /// 회전 오브젝트 스폰 담당. ISkillSpawner 구현.
    ///
    /// Executor가 SimultaneousSpread로 fireIndex별 Spawn()을 호출하면,
    /// 각 호출에서 OrbitalObject 1개를 균등 각도로 배치.
    /// 생성된 OrbitalObject는 자체 Update에서 궤도 위치를 관리.
    ///
    /// 스킬: 장검, 얼음 고리
    ///
    /// [Phase 7 리팩토링] Step 4-5
    /// </summary>
    public class OrbitalSpawner : ISkillSpawner
    {
        private readonly GameObject orbitalPrefab;

        public OrbitalSpawner(GameObject prefab)
        {
            orbitalPrefab = prefab;
        }

        public void Prewarm(SkillData data)
        {
            if (orbitalPrefab != null)
                PoolManager.Instance?.Prewarm(orbitalPrefab, data.objectCount * 2);
        }

        public void Cleanup()
        {
            // OrbitalObject는 자체 duration으로 소멸
        }

        public void Spawn(SpawnContext ctx)
        {
            if (orbitalPrefab == null) return;

            SkillData data = ctx.skillData;

            GameObject obj = PoolManager.Instance.Get(orbitalPrefab);
            var orbital = obj.GetComponent<OrbitalObject>();

            if (orbital == null)
            {
                Debug.LogError("[OrbitalSpawner] OrbitalObject 컴포넌트 없음");
                PoolManager.Instance.Return(obj);
                return;
            }

            // ── 스탯 계산 (SpawnContext에서 필터링 완료된 값 사용) ──
            float radius = data.orbitRadius + ctx.skillRangeBonus;
            float duration = data.areaDuration + ctx.skillDurationBonus;

            // areaDuration 미설정 시 안전장치 (SO에서 반드시 설정해야 함)
            if (duration <= 0f)
            {
                Debug.LogWarning($"[OrbitalSpawner] {data.skillName}: areaDuration이 0 — SO에서 설정 필요. 기본 2초 적용.");
                duration = 2f;
            }

            // 균등 각도 배치 (360° / totalCount)
            float baseAngle = (360f / ctx.totalCount) * ctx.fireIndex;

            // TwoPhase: duration 대신 1바퀴 완주 시 Phase2 전환
            bool fireOnOneRotation = data.firingMode == FiringMode.TwoPhase;

            orbital.Initialize(
                damage: ctx.damage,
                knockbackForce: ctx.knockbackForce,
                duration: duration,
                playerTransform: ctx.playerTransform,
                baseAngle: baseAngle,
                orbitRadius: radius,
                rotationSpeed: data.rotationSpeed,
                ownerTransform: ctx.playerTransform,
                fireOnOneRotation: fireOnOneRotation
            );

            // TwoPhase: Phase1 완료 콜백 연결 (각 orbital이 자기 위치/방향 전달)
            if (ctx.onSpawnComplete != null)
                orbital.SetOnComplete(ctx.onSpawnComplete);
        }
    }
}