using System.Collections.Generic;
using UnityEngine;
using SwDreams.Adapter.Manager;
using SwDreams.Data;

namespace SwDreams.Adapter.Skill
{
    /// <summary>
    /// 장판(지대) 스킬 효과.
    /// Execute() 호출 시 플레이어 위치에 장판을 생성.
    ///
    /// 사용 스킬:
    /// - 개미지옥: 피해 장판 (isHealingEffect = false)
    /// - 성역: 회복 장판 (isHealingEffect = true)
    ///
    /// SkillData 참조 필드:
    /// - areaRadius: 장판 반경
    /// - areaDuration: 장판 유지 시간 (+ PlayerStats.SkillDurationBonus)
    /// - tickRate: 판정 간격
    /// - isHealingEffect: true면 회복, false면 피해
    /// - maxInstances: 동시 최대 장판 수
    /// - effectPrefab: 장판 프리팹 (AreaZone 컴포넌트 필요)
    ///
    /// 네트워크: 장판은 로컬 비주얼, 데미지/회복 판정은 호스트만.
    /// </summary>
    public class AreaEffect : SkillEffect
    {
        private GameObject zonePrefab;
        private Transform playerTransform;
        private PlayerStats playerStats;

        // 활성 장판 추적 (maxInstances 관리)
        private List<GameObject> activeZones = new List<GameObject>();
        private int maxInstances;

        private void Start()
        {
            playerTransform = transform.root;
            playerStats = playerTransform.GetComponent<PlayerStats>();
        }

        /// <summary>
        /// SkillEffectFactory에서 생성 직후 호출.
        /// </summary>
        public void Initialize(SkillData data)
        {
            zonePrefab = data.effectPrefab;
            maxInstances = data.maxInstances;

            if (zonePrefab != null)
                PoolManager.Instance?.Prewarm(zonePrefab, maxInstances + 2);
            else
                Debug.LogWarning($"[AreaEffect] {data.skillName}: effectPrefab 미설정!");
        }

        public override void Execute(Skill skill)
        {
            if (zonePrefab == null || playerTransform == null) return;

            // 오래된 장판 정리
            CleanupDestroyedZones();

            // 최대 개수 초과 시 가장 오래된 장판 제거
            while (activeZones.Count >= maxInstances)
            {
                RemoveOldestZone();
            }

            // 장판 생성
            GameObject zoneObj = PoolManager.Instance.Get(zonePrefab);
            var zone = zoneObj.GetComponent<AreaZone>();

            if (zone == null)
            {
                Debug.LogError("[AreaEffect] AreaZone 컴포넌트 없음");
                PoolManager.Instance.Return(zoneObj);
                return;
            }

            SkillData data = skill.Data;

            // PlayerStats 보너스 적용
            float radius = data.areaRadius + (playerStats != null ? playerStats.SkillRangeBonus : 0f);
            float duration = data.areaDuration + (playerStats != null ? playerStats.SkillDurationBonus : 0f);

            int damage = skill.CurrentDamage;
            if (playerStats != null)
            {
                if (data.isHealingEffect)
                    damage = Mathf.RoundToInt(damage * playerStats.HealMultiplier);
                else
                    damage = Mathf.RoundToInt(damage * playerStats.AttackMultiplier);
            }

            zone.Initialize(
                position: playerTransform.position,
                damage: damage,
                radius: radius,
                duration: duration,
                tickRate: data.tickRate,
                isHealing: data.isHealingEffect
            );

            activeZones.Add(zoneObj);
        }

        /// <summary>
        /// 비활성화된 장판 참조 정리.
        /// </summary>
        private void CleanupDestroyedZones()
        {
            for (int i = activeZones.Count - 1; i >= 0; i--)
            {
                if (activeZones[i] == null || !activeZones[i].activeInHierarchy)
                    activeZones.RemoveAt(i);
            }
        }

        /// <summary>
        /// 가장 오래된 (리스트 앞) 장판을 풀에 반환.
        /// </summary>
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
