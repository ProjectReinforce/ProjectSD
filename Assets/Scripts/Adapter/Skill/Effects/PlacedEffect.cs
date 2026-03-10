using System.Collections.Generic;
using UnityEngine;
using SwDreams.Adapter.Manager;
using SwDreams.Data;

namespace SwDreams.Adapter.Skill
{
    /// <summary>
    /// 설치형 스킬 효과.
    /// Execute() 호출 시 플레이어 위치에 포탑을 설치.
    ///
    /// 사용 스킬:
    /// - 자동포탑: 고정 위치에서 자동 공격, 항상 치명타
    ///
    /// SkillData 참조 필드:
    /// - areaDuration: 포탑 유지 시간 (+ PlayerStats.SkillDurationBonus)
    /// - attackRange: 공격 사거리 (+ PlayerStats.SkillRangeBonus)
    /// - attackCooldown: 공격 간격
    /// - alwaysCritical: 항상 치명타 여부
    /// - maxInstances: 동시 최대 포탑 수
    /// - effectPrefab: 포탑 프리팹 (PlacedTurret 컴포넌트 필요)
    ///
    /// 네트워크: 로컬 비주얼 + 방향, 호스트 데미지 판정.
    /// </summary>
    public class PlacedEffect : SkillEffect
    {
        private GameObject turretPrefab;
        private Transform playerTransform;
        private PlayerStats playerStats;

        // 활성 포탑 추적
        private List<GameObject> activeTurrets = new List<GameObject>();
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
            turretPrefab = data.effectPrefab;
            maxInstances = data.maxInstances;

            if (turretPrefab != null)
                PoolManager.Instance?.Prewarm(turretPrefab, maxInstances + 2);
            else
                Debug.LogWarning($"[PlacedEffect] {data.skillName}: effectPrefab 미설정!");
        }

        public override void Execute(Skill skill)
        {
            if (turretPrefab == null || playerTransform == null) return;

            // 비활성 포탑 정리
            CleanupDestroyedTurrets();

            // 최대 개수 초과 시 가장 오래된 포탑 제거
            while (activeTurrets.Count >= maxInstances)
            {
                RemoveOldestTurret();
            }

            // 포탑 생성
            GameObject turretObj = PoolManager.Instance.Get(turretPrefab);
            var turret = turretObj.GetComponent<PlacedTurret>();

            if (turret == null)
            {
                Debug.LogError("[PlacedEffect] PlacedTurret 컴포넌트 없음");
                PoolManager.Instance.Return(turretObj);
                return;
            }

            SkillData data = skill.Data;

            // PlayerStats 보너스 적용
            float range = data.attackRange + (playerStats != null ? playerStats.SkillRangeBonus : 0f);
            float duration = data.areaDuration + (playerStats != null ? playerStats.SkillDurationBonus : 0f);
            float critMul = playerStats != null ? playerStats.CritDamageMultiplier : 1.5f;

            int damage = skill.CurrentDamage;
            if (playerStats != null)
                damage = Mathf.RoundToInt(damage * playerStats.AttackMultiplier);

            turret.Initialize(
                position: playerTransform.position,
                damage: damage,
                attackRange: range,
                attackCooldown: data.attackCooldown,
                duration: duration,
                alwaysCritical: data.alwaysCritical,
                critDamageMultiplier: critMul
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
