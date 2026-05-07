using System;
using SwDreams.Features.Character.Adapter;
using SwDreams.Features.Skill.Domain.ValueObjects;
using SwDreams.Features.Skill.Adapter.Data;
using SwDreams.Features.Skill.Adapter;
using Photon.Pun;
using UnityEngine;
using SwDreams.Shared.Data;
using SwDreams.Shared.Managers;
using SwDreams.Features.Skill.Adapter.TriggerEffects;

namespace SwDreams.Features.Skill.Adapter
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
        private ISkillSpawner phase2Spawner; // TwoPhase Phase2용
        private GameObject executorPrefab;

        // PlayerStats 캐시 (CDR 적용용)
        private PlayerStats cachedStats;

        // [N15/N17] 자기 PlayerStub PhotonView 캐시. 자기측만 자체 fire(cooldown+Fire), 다른 클라 측은 RPC 만 받아 처리.
        private PhotonView cachedPV;

        // TriggerSystem 캐시
        private SkillTriggerSystem triggerSystem;

        // 로컬 플레이어 전용 플래그
        private bool isActive = false;

        private void Awake()
        {
            cachedStats = GetComponentInParent<PlayerStats>();
            cachedPV = GetComponentInParent<PhotonView>();
        }

        /// <summary>
        /// 스킬 활성화. 로컬 플레이어에서만 호출.
        /// </summary>
        public void Activate(SkillData data, ISkillSpawner spawner, GameObject executorPrefab,
            ISkillSpawner phase2Spawner = null)
        {
            skillData = data;
            this.spawner = spawner;
            this.phase2Spawner = phase2Spawner;
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

            // [N15/N17] 자기 PlayerStub 만 자체 cooldown+Fire. 다른 클라 측은 RPC 만 수신.
            // 이 가드 없으면 모든 클라가 자체 시뮬레이션 + RPC 도착 → 이중 spawn (다른 사람 스킬 2번 발사 버그).
            if (cachedPV != null && !cachedPV.IsMine) return;

            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentState != GameManager.GameState.Playing &&
                GameManager.Instance.CurrentState != GameManager.GameState.BossFight)
                return;

            // R8: 첫 적 스폰 가능 시점 전에는 발동 차단 (호스트/클라 동시성 차이 제거).
            if (SpawnManager.Instance != null && !SpawnManager.Instance.IsReady)
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

            // TwoPhase: Phase2 Spawner 설정 (Begin 이후 — Begin에서 phase2Spawner를 null 초기화하므로)
            if (phase2Spawner != null)
                executor.SetPhase2Spawner(phase2Spawner);

            // OnFire 트리거
            // 인-런 통계 (B-1a — run-statistics.md §4): attacker / sourceSkillId 주입
            int attackerActor = 0;
            bool isMineFire = false;
            var rootPv = transform.root.GetComponent<Photon.Pun.PhotonView>();
            if (rootPv != null && rootPv.Owner != null)
            {
                attackerActor = rootPv.Owner.ActorNumber;
                isMineFire = rootPv.IsMine;
            }

            if (triggerSystem != null && triggerSystem.HasTrigger(TriggerType.OnFire))
            {
                triggerSystem.FireTrigger(TriggerType.OnFire, new Domain.ValueObjects.TriggerContext
                {
                    position = transform.root.position,
                    owner = transform.root,
                    attackerActorNumber = attackerActor,
                    sourceSkillId = skillData != null ? skillData.skillId : 0
                });
            }

            // B-1a: 자기 발사 카운트 — 자기 root PV 만. RPC 도착 경로 (다른 클라 발사) 는 제외.
            if (isMineFire && skillData != null)
            {
                SwDreams.Features.Stats.Adapter.LocalStatsRecorder.Instance?
                    .OnFire(skillData.skillId);
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

        // ===== [N15/N17] 네트워크 발사 (RPC 수신 측) =====

        /// <summary>
        /// PlayerStub.RPC 가 도착해 SkillManager.HandleNetworkSkillSpawn 가 호출.
        /// 단발 발사 (cooldown 무관, RPC 송신 없음 — 이미 RPC 도착).
        /// 호스트 측은 데미지 권위, 다른 클라 측은 시각만.
        /// </summary>
        public void FireFromNetwork(Vector2 baseDir, Vector2 spawnPos, bool hasSpawnPosOverride, int fireIndex, int totalCount)
        {
            if (skillData == null || spawner == null || executorPrefab == null) return;
            if (!isActive) return;

            // TriggerSystem lazy cache
            if (triggerSystem == null)
                triggerSystem = GetComponent<SkillTriggerSystem>();

            GameObject executorObj = PoolManager.Instance.Get(executorPrefab);
            var executor = executorObj.GetComponent<SkillExecutor>();
            if (executor == null)
            {
                Debug.LogError($"[Skill] {skillData.skillName}: SkillExecutor 컴포넌트 없음 (FireFromNetwork)");
                PoolManager.Instance.Return(executorObj);
                return;
            }

            executor.BeginFromNetwork(this, spawner, cachedStats, transform.root, triggerSystem,
                baseDir, spawnPos, hasSpawnPosOverride, fireIndex, totalCount);
        }
    }
}