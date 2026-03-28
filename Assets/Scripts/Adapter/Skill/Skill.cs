using System;
using UnityEngine;
using SwDreams.Data;
using SwDreams.Adapter.Manager;
using SwDreams.Adapter.Skill.TriggerEffects;
using SwDreams.Domain.ValueObjects;

namespace SwDreams.Adapter.Skill
{
    /// <summary>
    /// 스킬 슬롯. 쿨다운 관리 + Executor 생성.
    /// 서바이벌라이크 특성상 자동 발동.
    ///
    /// [Step 4-6] SkillEffect 레이어 제거.
    /// Fire() → Executor → Spawner 직통 구조.
    ///
    /// Player(또는 PlayerStub)의 자식 오브젝트에 부착.
    /// 로컬 플레이어에서만 동작 (원격 플레이어의 스킬은 비활성).
    /// </summary>
    public class Skill : MonoBehaviour
    {
        [SerializeField] private SkillData skillData;

        // 상태
        public SkillData Data => skillData;
        public int Level { get; private set; } = 1;
        public bool IsMaxLevel => skillData != null && Level >= skillData.maxLevel;
        public float CooldownRemaining { get; private set; }
        public bool IsReady => CooldownRemaining <= 0f;

        // 현재 레벨 기준 스탯
        public int CurrentDamage => skillData.GetDamageForLevel(Level);
        public float CurrentCooldown => skillData.GetCooldownForLevel(Level);

        // 이벤트
        public event Action<Skill> OnFired;
        public event Action<Skill> OnLevelChanged;

        // [Step 4-6] Executor 직접 호출
        private ISkillSpawner spawner;
        private GameObject executorPrefab;

        // PlayerStats 캐시 (CDR 적용용)
        private PlayerStats cachedStats;

        // TriggerSystem 캐시
        private SkillTriggerSystem triggerSystem;

        // 로컬 플레이어 전용 플래그
        private bool isActive = false;

        private void Awake()
        {
            cachedStats = GetComponentInParent<PlayerStats>();
        }

        /// <summary>
        /// 스킬 활성화. 로컬 플레이어에서만 호출.
        /// </summary>
        public void Activate(SkillData data, ISkillSpawner spawner, GameObject executorPrefab)
        {
            skillData = data;
            this.spawner = spawner;
            this.executorPrefab = executorPrefab;
            Level = 1;
            CooldownRemaining = 0f;
            isActive = true;

            if (cachedStats == null)
                cachedStats = GetComponentInParent<PlayerStats>();
        }

        public void Deactivate()
        {
            isActive = false;
        }

        /// <summary>
        /// 레벨/데이터 유지한 채 활성화만 재개. ResumeAllSkills용.
        /// Activate와 달리 Level을 1로 리셋하지 않음.
        /// </summary>
        public void Resume()
        {
            isActive = true;
        }

        private void Update()
        {
            if (!isActive || skillData == null) return;

            // 패시브/혼돈은 spawner가 없음 — 쿨다운 체크 불필요
            if (spawner == null) return;

            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentState != GameManager.GameState.Playing &&
                GameManager.Instance.CurrentState != GameManager.GameState.BossFight)
                return;

            if (CooldownRemaining > 0f)
            {
                CooldownRemaining -= Time.deltaTime;
                return;
            }

            Fire();
        }

        private void Fire()
        {
            // CDR + 혼돈 쿨다운 배율
            float cooldown = CurrentCooldown;
            if (cachedStats != null)
                cooldown = cachedStats.GetEffectiveCooldown(cooldown);

            CooldownRemaining = cooldown;

            // Executor를 풀에서 꺼내서 시작
            if (executorPrefab == null)
            {
                Debug.LogWarning($"[Skill] {skillData.skillName}: executorPrefab 미설정");
                return;
            }

            // TriggerSystem lazy cache
            if (triggerSystem == null)
                triggerSystem = GetComponent<SkillTriggerSystem>();

            GameObject executorObj = PoolManager.Instance.Get(executorPrefab);
            var executor = executorObj.GetComponent<SkillExecutor>();

            if (executor == null)
            {
                Debug.LogError($"[Skill] {skillData.skillName}: SkillExecutor 컴포넌트 없음");
                PoolManager.Instance.Return(executorObj);
                return;
            }

            executor.Begin(this, spawner, cachedStats, transform.root, triggerSystem);

            // OnFire 트리거
            if (triggerSystem != null && triggerSystem.HasTrigger(TriggerType.OnFire))
            {
                triggerSystem.FireTrigger(TriggerType.OnFire, new Domain.ValueObjects.TriggerContext
                {
                    position = transform.root.position,
                    owner = transform.root
                });
            }

            OnFired?.Invoke(this);
        }

        public void LevelUp()
        {
            if (IsMaxLevel) return;
            Level++;
            OnLevelChanged?.Invoke(this);
            Debug.Log($"[Skill] {skillData.skillName} → Lv.{Level}");
        }
    }
}