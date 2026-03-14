using System.Collections.Generic;
using UnityEngine;
using SwDreams.Adapter.Manager;
using SwDreams.Data;

namespace SwDreams.Adapter.Skill
{
    /// <summary>
    /// 회전형 스킬 효과.
    /// Execute() 호출 시 플레이어 주변에 회전 오브젝트를 생성.
    /// 쿨다운마다 새 웨이브 생성 (서바이벌 장르 패턴).
    ///
    /// 사용 스킬:
    /// - 장검: 플레이어 주변 회전 + 접촉 데미지 + 넉백
    ///
    /// SkillData 참조 필드:
    /// - orbitRadius: 궤도 반경 (+ PlayerStats.SkillRangeBonus)
    /// - rotationSpeed: 회전 속도 (도/초)
    /// - objectCount: 오브젝트 개수
    /// - areaDuration: 웨이브 유지 시간 (+ PlayerStats.SkillDurationBonus)
    /// - knockbackForce: 넉백 힘 (× PlayerStats.KnockbackMultiplier)
    /// - effectPrefab: 회전 오브젝트 프리팹 (OrbitalObject 컴포넌트 필요)
    ///
    /// 네트워크: 로컬 비주얼 + 위치 계산, 호스트 데미지 판정.
    /// </summary>
    public class OrbitalEffect : SkillEffect
    {
        private GameObject orbitalPrefab;
        private Transform playerTransform;
        private PlayerStats playerStats;

        // 현재 활성 오브젝트 목록
        private List<OrbitalInstance> activeOrbitals = new List<OrbitalInstance>();

        // 회전 각도 (Update에서 누적)
        private float currentAngle;

        /// <summary>
        /// 개별 궤도 오브젝트 추적 정보.
        /// </summary>
        private struct OrbitalInstance
        {
            public GameObject obj;
            public float baseAngle; // 이 오브젝트의 궤도 시작 각도
        }

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
            orbitalPrefab = data.effectPrefab;

            if (orbitalPrefab != null)
                PoolManager.Instance?.Prewarm(orbitalPrefab, data.objectCount * 2);
            else
                Debug.LogWarning($"[OrbitalEffect] {data.skillName}: effectPrefab 미설정!");
        }

        public override void Execute(Skill skill)
        {
            if (orbitalPrefab == null || playerTransform == null) return;

            SkillData data = skill.Data;

            // PlayerStats 보너스 적용
            int damage = skill.CurrentDamage;
            float knockback = data.knockbackForce;
            float duration = data.areaDuration;

            if (playerStats != null)
            {
                damage = Mathf.RoundToInt(damage * playerStats.AttackMultiplier);
                knockback *= playerStats.KnockbackMultiplier;
                duration += playerStats.SkillDurationBonus;
            }

            int count = data.objectCount;

            // 균등 배치 (360° / count)
            float angleStep = 360f / count;

            for (int i = 0; i < count; i++)
            {
                GameObject obj = PoolManager.Instance.Get(orbitalPrefab);
                var orbital = obj.GetComponent<OrbitalObject>();

                if (orbital == null)
                {
                    Debug.LogError("[OrbitalEffect] OrbitalObject 컴포넌트 없음");
                    PoolManager.Instance.Return(obj);
                    continue;
                }

                orbital.Initialize(damage, knockback, duration);

                float baseAngle = i * angleStep;
                activeOrbitals.Add(new OrbitalInstance
                {
                    obj = obj,
                    baseAngle = baseAngle
                });
            }
        }

        private void Update()
        {
            if (playerTransform == null) return;

            // 게임 일시정지 시 정지
            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentState != GameManager.GameState.Playing &&
                GameManager.Instance.CurrentState != GameManager.GameState.BossFight)
                return;

            // 스킬 데이터에서 회전 속도 가져오기 (Skill 컴포넌트 참조)
            var skill = GetComponent<Skill>();
            float rotSpeed = skill != null && skill.Data != null
                ? skill.Data.rotationSpeed
                : 180f;

            float radius = skill != null && skill.Data != null
                ? skill.Data.orbitRadius
                : 1.5f;

            if (playerStats != null)
                radius += playerStats.SkillRangeBonus;

            // 각도 누적
            currentAngle += rotSpeed * Time.deltaTime;
            if (currentAngle >= 360f) currentAngle -= 360f;

            // 비활성 오브젝트 정리 + 위치 업데이트
            for (int i = activeOrbitals.Count - 1; i >= 0; i--)
            {
                var inst = activeOrbitals[i];

                if (inst.obj == null || !inst.obj.activeInHierarchy)
                {
                    activeOrbitals.RemoveAt(i);
                    continue;
                }

                // 원형 궤도 위치 계산
                float angle = (currentAngle + inst.baseAngle) * Mathf.Deg2Rad;
                Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                inst.obj.transform.position = (Vector2)playerTransform.position + offset;

                // 오브젝트 회전 (진행 방향으로)
                float rotZ = (currentAngle + inst.baseAngle) + 90f; // 접선 방향
                inst.obj.transform.rotation = Quaternion.Euler(0, 0, rotZ);
            }
        }
    }
}
