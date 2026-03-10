using System;
using UnityEngine;
using SwDreams.Data;
using SwDreams.Adapter.Manager;

namespace SwDreams.Adapter.Skill
{
    /// <summary>
    /// 스킬 슬롯. 쿨다운 관리 + SkillEffect에 실행 위임.
    /// 서바이벌라이크 특성상 자동 발동.
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

        // 실행 담당
        private SkillEffect skillEffect;

        // PlayerStats 캐시 (CDR 적용용)
        private PlayerStats cachedStats;
        private ChaosSkillManager cachedChaos;

        // 로컬 플레이어 전용 플래그
        private bool isActive = false;

        private void Awake()
        {
            skillEffect = GetComponent<SkillEffect>();
            cachedStats = GetComponentInParent<PlayerStats>();
            cachedChaos = GetComponentInParent<ChaosSkillManager>();
            if (cachedChaos == null)
                cachedChaos = GetComponentInChildren<ChaosSkillManager>();
        }

        /// <summary>
        /// 스킬 활성화. 로컬 플레이어에서만 호출.
        /// </summary>
        public void Activate(SkillData data, SkillEffect effect = null)
        {
            skillData = data;
            Level = 1;
            CooldownRemaining = 0f;
            isActive = true;

            if (effect != null)
                skillEffect = effect;

            if (cachedStats == null)
                cachedStats = GetComponentInParent<PlayerStats>();
            if (cachedChaos == null)
            {
                cachedChaos = GetComponentInParent<ChaosSkillManager>();
                if (cachedChaos == null)
                    cachedChaos = GetComponentInChildren<ChaosSkillManager>();
            }
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
            if (!isActive || skillData == null || skillEffect == null) return;

            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentState != GameManager.GameState.Playing)
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
            // [Phase 5] PlayerStats CDR + Chaos CDR 적용
            float cooldown = CurrentCooldown;
            if (cachedStats != null)
                cooldown = cachedStats.GetEffectiveCooldown(cooldown);
            if (cachedChaos != null)
                cooldown *= cachedChaos.ChaosCooldownMultiplier;

            CooldownRemaining = cooldown;
            skillEffect.Execute(this);
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
