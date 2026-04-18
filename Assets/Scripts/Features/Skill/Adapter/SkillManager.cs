using System;
using SwDreams.Features.Progression.Adapter;
using SwDreams.Features.Character.Adapter;
using SwDreams.Features.Skill.Domain.ValueObjects;
using SwDreams.Features.Skill.Adapter.Data;
using SwDreams.Features.Skill.Adapter;
using System.Collections.Generic;
using UnityEngine;
using SwDreams.Shared.Data;
using SwDreams.Shared.Managers;
using SwDreams.Features.Skill.Adapter.TriggerEffects;

namespace SwDreams.Features.Skill.Adapter
{
    /// <summary>
    /// 플레이어 스킬 6슬롯 관리.
    /// 각 플레이어의 자식 오브젝트에 부착.
    ///
    /// 역할:
    /// - 스킬 획득/레벨업/제거
    /// - 슬롯 제한 관리 (액티브 + 패시브 합계)
    /// - 진화 가능 여부 감지
    /// - 선택지 생성 (호스트용)
    /// - 패시브 스킬 → PlayerStats 반영
    ///
    /// [Step 4-6] SkillSpawnerFactory 도입. SkillEffect 레이어 제거.
    /// Skill.Fire() → Executor → Spawner 직통 구조.
    /// </summary>
    public class SkillManager : MonoBehaviour
    {
        /// <summary>
        /// 진화 대기 정보. 어떤 2개 스킬이 어떤 진화 스킬로 변할 수 있는지.
        /// </summary>
        public struct EvolutionCandidate
        {
            public int activeSkillId;
            public int passiveSkillId;
            public SkillData evolvedSkillData;
        }
        
        // ===== 설정 =====
        // [CHANGED] GameManager.Instance.Config에서 읽되, null이면 기본값 6 사용
        private const int DefaultMaxSlots = 6;
        public int MaxSlots
        {
            get
            {
                var cfg = GameManager.Instance?.Config;
                return (cfg != null) ? cfg.maxSkillSlots : DefaultMaxSlots;
            }
        }

        [Header("스킬 프리팹")]
        [SerializeField] private GameObject skillSlotPrefab;
        // 빈 오브젝트에 Skill 컴포넌트만 붙은 프리팹.

        [SerializeField] private GameObject executorPrefab;
        // 빈 오브젝트에 SkillExecutor 컴포넌트만 붙은 프리팹.
        // PoolManager에서 풀링.

        // ===== 상태 =====
        private List<Skill> equippedSkills = new List<Skill>();
        private List<EvolutionCandidate> pendingEvolutions = new List<EvolutionCandidate>();

        // [Step 4-6] SkillSpawnerFactory. Awake()에서 초기화.
        private SkillSpawnerFactory spawnerFactory;

        // [Step 1-3] PlayerStats 캐시. 패시브 modifier 직접 등록용.
        private PlayerStats cachedStats;

        // 외부 읽기용
        public IReadOnlyList<Skill> EquippedSkills => equippedSkills;
        public int SlotCount => equippedSkills.Count;
        public int EmptySlots => MaxSlots - equippedSkills.Count;
        public bool HasEmptySlot => equippedSkills.Count < MaxSlots;

        // ===== 이벤트 =====
        /// <summary>스킬 추가 시 발생. UI 갱신용.</summary>
        public event Action<Skill> OnSkillAdded;

        /// <summary>스킬 레벨업 시 발생. UI 갱신용.</summary>
        public event Action<Skill> OnSkillLeveledUp;

        /// <summary>스킬 제거 시 발생 (진화 시). UI 갱신용.</summary>
        public event Action<int> OnSkillRemoved; // skillId

        /// <summary>진화 발생 시. 연출용.</summary>
        public event Action<SkillData> OnEvolution; // 진화 결과 스킬

        // ===== 초기화 =====

        // [Step 4-6] Awake에서 팩토리 초기화
        private void Awake()
        {
            spawnerFactory = new SkillSpawnerFactory();
            spawnerFactory.RegisterDefaults();

            // Executor 프리팹 프리웜
            if (executorPrefab != null)
                PoolManager.Instance?.Prewarm(executorPrefab, 5);

            cachedStats = GetComponentInParent<PlayerStats>();
        }

        // ===== Config 접근 헬퍼 =====

        /// <summary>
        /// GameplayConfig 단축 접근. null 가능.
        /// 내부에서 반복 사용 시 로컬 변수에 캐싱 권장:
        ///   var cfg = GetConfig();
        /// </summary>
        private GameplayConfig GetConfig()
        {
            return GameManager.Instance?.Config;
        }

        // ===== PlayerStats 접근 헬퍼 =====

        /// <summary>
        /// PlayerStats 단축 접근. Awake에서 캐시되나 안전을 위해 lazy init 포함.
        /// </summary>
        private PlayerStats GetStats()
        {
            if (cachedStats == null)
                cachedStats = GetComponentInParent<PlayerStats>();
            return cachedStats;
        }

        // ===== 스킬 조회 =====

        /// <summary>
        /// 특정 스킬 ID를 보유 중인지 확인.
        /// </summary>
        public bool HasSkill(int skillId)
        {
            for (int i = 0; i < equippedSkills.Count; i++)
            {
                if (equippedSkills[i].Data.skillId == skillId)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 스킬 ID로 장착된 Skill 인스턴스 반환. 없으면 null.
        /// </summary>
        public Skill GetSkill(int skillId)
        {
            for (int i = 0; i < equippedSkills.Count; i++)
            {
                if (equippedSkills[i].Data.skillId == skillId)
                    return equippedSkills[i];
            }
            return null;
        }

        /// <summary>
        /// 보유 중이면서 아직 최대 레벨이 아닌 스킬 목록.
        /// 슬롯 꽉 찼을 때 선택지 생성용.
        /// </summary>
        public List<Skill> GetUpgradeableSkills()
        {
            var result = new List<Skill>();
            for (int i = 0; i < equippedSkills.Count; i++)
            {
                if (!equippedSkills[i].IsMaxLevel)
                    result.Add(equippedSkills[i]);
            }
            return result;
        }

        /// <summary>
        /// 보유 중인 특정 타입 스킬 목록.
        /// </summary>
        public List<Skill> GetSkillsByType(SkillType type)
        {
            var result = new List<Skill>();
            for (int i = 0; i < equippedSkills.Count; i++)
            {
                if (equippedSkills[i].Data.skillType == type)
                    result.Add(equippedSkills[i]);
            }
            return result;
        }

        /// <summary>
        /// 외부(LevelUpManager)에서 진화 후보 조회용.
        /// </summary>
        public List<EvolutionCandidate> GetPendingEvolutions()
        {
            return pendingEvolutions;
        }

        // ===== 스킬 획득 =====

        /// <summary>
        /// 새 스킬 획득. 빈 슬롯이 있어야 함.
        /// 이미 보유 중이면 레벨업으로 처리.
        /// </summary>
        /// <returns>true: 성공, false: 슬롯 부족 또는 오류</returns>
        public bool AcquireSkill(SkillData skillData)
        {
            if (skillData == null)
            {
                Debug.LogError("[SkillManager] AcquireSkill: skillData가 null");
                return false;
            }

            // 이미 보유 중이면 레벨업
            Skill existing = GetSkill(skillData.skillId);
            if (existing != null)
            {
                return LevelUpSkill(skillData.skillId);
            }

            // 빈 슬롯 체크
            if (!HasEmptySlot)
            {
                Debug.LogWarning($"[SkillManager] 슬롯 부족! ({SlotCount}/{MaxSlots})");
                return false;
            }

            // 새 스킬 슬롯 생성
            Skill newSkill = CreateSkillSlot(skillData);
            if (newSkill == null) return false;

            equippedSkills.Add(newSkill);
            OnSkillAdded?.Invoke(newSkill);

            // [Step 1-3] 패시브면 PlayerStats에 modifier 직접 등록
            if (skillData.skillType == SkillType.Passive)
            {
                var stats = GetStats();
                if (stats != null)
                {
                    stats.RegisterPassive(skillData, 1);
                    stats.Recalculate();
                }
            }

            Debug.Log($"[SkillManager] 스킬 획득: {skillData.skillName} (슬롯 {SlotCount}/{MaxSlots})");
            return true;
        }

        // ===== 스킬 레벨업 =====

        /// <summary>
        /// 기존 스킬 레벨업.
        /// </summary>
        /// <returns>true: 성공, false: 미보유 또는 최대 레벨</returns>
        public bool LevelUpSkill(int skillId)
        {
            Skill skill = GetSkill(skillId);
            if (skill == null)
            {
                Debug.LogWarning($"[SkillManager] 레벨업 실패: 스킬 ID {skillId} 미보유");
                return false;
            }

            if (skill.IsMaxLevel)
            {
                Debug.LogWarning($"[SkillManager] 레벨업 실패: {skill.Data.skillName} 이미 최대 레벨");
                return false;
            }

            skill.LevelUp();
            OnSkillLeveledUp?.Invoke(skill);

            // [Step 1-3] 패시브면 PlayerStats modifier 갱신
            if (skill.Data.skillType == SkillType.Passive)
            {
                var stats = GetStats();
                if (stats != null)
                {
                    stats.RegisterPassive(skill.Data, skill.Level);
                    stats.Recalculate();
                }
            }

            // 진화 가능 체크 (양방향)
            // 방금 maxLevel 도달한 스킬 자체 + 이 스킬을 partner로 갖는 다른 스킬도 확인
            CheckEvolution(skill);
            if (skill.IsMaxLevel)
                CheckEvolutionAsPartner(skill);

            return true;
        }

        // ===== 스킬 제거 (진화 시 사용) =====

        /// <summary>
        /// 스킬 제거. 진화 시 기존 2개 스킬 제거용.
        /// 인덱스 꼬임 방지를 위해 ID로 제거.
        /// </summary>
        private bool RemoveSkill(int skillId)
        {
            for (int i = equippedSkills.Count - 1; i >= 0; i--)
            {
                if (equippedSkills[i].Data.skillId == skillId)
                {
                    Skill skill = equippedSkills[i];

                    // [Step 1-3] 패시브면 modifier 제거 (진화 승계 시에는 이미 rename되어 no-op)
                    if (skill.Data.skillType == SkillType.Passive)
                    {
                        var stats = GetStats();
                        stats?.UnregisterPassive(skillId);
                    }

                    equippedSkills.RemoveAt(i);
                    skill.Deactivate();
                    Destroy(skill.gameObject);
                    OnSkillRemoved?.Invoke(skillId);
                    return true;
                }
            }
            return false;
        }

        // ===== 진화 시스템 =====

        /// <summary>
        /// 스킬 레벨업 후 진화 가능 여부 체크.
        /// SkillData에 evolutionPair / evolvedSkill이 설정돼 있고,
        /// 둘 다 최대 레벨이면 진화 발동.
        /// </summary>
        private void CheckEvolution(Skill skill)
        {
            if (skill.Data.evolutionPair == null || skill.Data.evolvedSkill == null)
                return;

            Skill partner = GetSkill(skill.Data.evolutionPair.skillId);
            if (partner == null || !partner.IsMaxLevel || !skill.IsMaxLevel)
                return;

            // 이미 같은 진화가 대기열에 있는지 확인
            int evolvedId = skill.Data.evolvedSkill.skillId;
            for (int i = 0; i < pendingEvolutions.Count; i++)
            {
                if (pendingEvolutions[i].evolvedSkillData.skillId == evolvedId)
                    return;
            }

            // 어느 쪽이 액티브인지 판별
            int activeId, passiveId;
            if (skill.Data.skillType == SkillType.Active)
            {
                activeId = skill.Data.skillId;
                passiveId = partner.Data.skillId;
            }
            else
            {
                activeId = partner.Data.skillId;
                passiveId = skill.Data.skillId;
            }

            pendingEvolutions.Add(new EvolutionCandidate
            {
                activeSkillId = activeId,
                passiveSkillId = passiveId,
                evolvedSkillData = skill.Data.evolvedSkill
            });

            Debug.Log($"[SkillManager] ★ 진화 가능 등록: {skill.Data.skillName} + {partner.Data.skillName} → {skill.Data.evolvedSkill.skillName}");
        }

        /// <summary>
        /// 방금 maxLevel에 도달한 스킬을 evolutionPair로 갖는 다른 스킬이 있는지 역방향 체크.
        /// SO 한쪽에만 evolutionPair가 설정된 경우 누락 방지.
        /// </summary>
        private void CheckEvolutionAsPartner(Skill justMaxed)
        {
            for (int i = 0; i < equippedSkills.Count; i++)
            {
                Skill other = equippedSkills[i];
                if (other == justMaxed) continue;
                if (!other.IsMaxLevel) continue;
                if (other.Data.evolutionPair == null || other.Data.evolvedSkill == null) continue;
                if (other.Data.evolutionPair.skillId != justMaxed.Data.skillId) continue;

                // other가 justMaxed를 partner로 갖고, 둘 다 maxLevel → CheckEvolution 위임
                CheckEvolution(other);
            }
        }

        // [Phase 5] 진화로 제거된 스킬 ID 추적 (선택지에서 제외용)
        private HashSet<int> removedByEvolutionIds = new HashSet<int>();

        /// <summary>
        /// 플레이어가 진화 스킬을 선택했을 때 호출.
        /// 기존 2개 스킬 제거 + 진화 스킬 1개 생성.
        /// </summary>
        public bool TryExecuteEvolution(int evolvedSkillId)
        {
            EvolutionCandidate? target = null;
            int targetIndex = -1;

            for (int i = 0; i < pendingEvolutions.Count; i++)
            {
                if (pendingEvolutions[i].evolvedSkillData.skillId == evolvedSkillId)
                {
                    target = pendingEvolutions[i];
                    targetIndex = i;
                    break;
                }
            }

            if (target == null)
            {
                Debug.LogError($"[SkillManager] 진화 실행 실패 — ID {evolvedSkillId} 대기열에 없음");
                return false;
            }

            var evo = target.Value;

            // 진화로 제거되는 스킬 ID 기록 (선택지 재등장 방지)
            removedByEvolutionIds.Add(evo.activeSkillId);
            removedByEvolutionIds.Add(evo.passiveSkillId);

            // [Step 1-3] 패시브 modifier를 진화 스킬로 승계
            // RemoveSkill보다 먼저 호출 — source를 rename하여 UnregisterPassive가 no-op이 되게 함
            var stats = GetStats();
            stats?.PreservePassiveForEvolution(evo.passiveSkillId, evolvedSkillId);

            // 기존 2개 스킬 제거
            RemoveSkill(evo.activeSkillId);
            RemoveSkill(evo.passiveSkillId);

            // 진화 스킬 생성 + 리스트에 추가
            Skill evolvedSkill = CreateSkillSlot(evo.evolvedSkillData);
            if (evolvedSkill != null)
                equippedSkills.Add(evolvedSkill);

            // 대기열에서 제거
            pendingEvolutions.RemoveAt(targetIndex);

            // [Step 1-3] 스탯 재계산 (패시브 승계 반영)
            stats?.Recalculate();

            Debug.Log($"[SkillManager] ★ 진화 완료: {evo.evolvedSkillData.skillName} (슬롯 {SlotCount}/{MaxSlots})");

            OnEvolution?.Invoke(evo.evolvedSkillData);
            return true;
        }

        /// <summary>
        /// 외부에서 진화 가능한 조합이 있는지 확인.
        /// 선택지 생성 시 진화 선택지 우선 표시용.
        /// </summary>
        public SkillData GetAvailableEvolution()
        {
            for (int i = 0; i < equippedSkills.Count; i++)
            {
                Skill skill = equippedSkills[i];
                if (!skill.IsMaxLevel) continue;

                SkillData data = skill.Data;
                if (data.evolutionPair == null || data.evolvedSkill == null) continue;

                Skill partner = GetSkill(data.evolutionPair.skillId);
                if (partner != null && partner.IsMaxLevel)
                    return data.evolvedSkill;
            }
            return null;
        }

        // ===== 선택지 생성 (호스트용) =====

        /// <summary>
        /// 이 플레이어의 상태에 맞는 레벨업 선택지를 생성.
        /// LevelUpManager.SendNormalChoices()에서 호출.
        /// </summary>
        /// <param name="pool">SkillDatabase.GetNormalPool() 결과</param>
        /// <param name="count">선택지 개수 (기본 3, Config 우선)</param>
        /// <param name="evolutionChance">진화 등장 확률 (기본 0.7, Config 우선)</param>
        public SkillData[] GenerateChoices(SkillData[] pool, int count = 3, float evolutionChance = 0.7f)
        {
            // [CHANGED] GameplayConfig 값 우선 사용
            var cfg = GetConfig();
            if (cfg != null)
            {
                count = cfg.choiceCount;
                evolutionChance = cfg.evolutionChance;
            }

            // 1) 진화 후보 수집 — 진화 가능하면 항상 선택지에 포함
            SkillData evolutionChoice = null;
            if (pendingEvolutions.Count > 0)
            {
                int evoIndex = UnityEngine.Random.Range(0, pendingEvolutions.Count);
                evolutionChoice = pendingEvolutions[evoIndex].evolvedSkillData;
            }

            // 2) 일반 후보 수집 (최대 레벨 제외, 슬롯 꽉 차면 미보유 제외)
            List<SkillData> normalCandidates = new List<SkillData>();
            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i] == null) continue;

                // 진화 스킬과 중복 방지
                if (evolutionChoice != null && pool[i].skillId == evolutionChoice.skillId)
                    continue;

                // [Phase 5] 진화로 제거된 스킬은 다시 등장하지 않음
                if (removedByEvolutionIds.Contains(pool[i].skillId))
                    continue;

                // 보유 중이고 최대 레벨이면 제외
                if (HasSkill(pool[i].skillId))
                {
                    var existing = GetSkill(pool[i].skillId);
                    if (existing.IsMaxLevel) continue;
                }
                // 슬롯 꽉 찼으면 미보유 스킬 제외
                else if (!HasEmptySlot)
                {
                    continue;
                }

                normalCandidates.Add(pool[i]);
            }

            // 3) 셔플
            ShuffleList(normalCandidates);

            // 4) 선택지 조합
            List<SkillData> result = new List<SkillData>();

            if (evolutionChoice != null)
                result.Add(evolutionChoice);

            for (int i = 0; i < normalCandidates.Count && result.Count < count; i++)
                result.Add(normalCandidates[i]);

            // 5) 최종 셔플 (진화가 항상 첫 자리가 아니도록)
            ShuffleList(result);

            return result.ToArray();
        }

        private void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }

        /// <summary>
        /// 혼돈 스킬 선택지 생성 (Lv.5, 10, 15 전용).
        /// </summary>
        /// <param name="chaosSkills">혼돈 스킬 풀</param>
        /// <param name="count">선택지 수 (기본 3, Config 우선)</param>
        public SkillData[] GenerateChaosChoices(SkillData[] chaosSkills, int count = 3)
        {
            var cfg = GetConfig();
            if (cfg != null)
                count = cfg.choiceCount;

            // [Phase 5] 이미 보유한 혼돈 스킬 제외
            var chaosManager = GetComponent<ChaosSkillManager>();
            if (chaosManager == null)
                chaosManager = GetComponentInParent<ChaosSkillManager>();

            List<SkillData> candidates = new List<SkillData>();
            foreach (var skill in chaosSkills)
            {
                if (skill == null) continue;
                if (chaosManager != null && chaosManager.HasChaosEffect(skill.chaosEffectType))
                    continue;
                candidates.Add(skill);
            }

            int resultCount = Mathf.Min(count, candidates.Count);
            SkillData[] result = new SkillData[resultCount];

            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            for (int i = 0; i < resultCount; i++)
                result[i] = candidates[i];

            return result;
        }

        /// <summary>
        /// 선택지에서 플레이어가 고른 스킬을 적용.
        /// 호스트가 결과를 받아 각 플레이어에서 호출.
        /// </summary>
        public void ApplyChoice(SkillData chosenSkill)
        {
            // [Phase 5] 혼돈 스킬이면 ChaosSkillManager에 위임 (슬롯 미사용)
            if (chosenSkill.skillType == SkillType.Chaos)
            {
                var chaosManager = GetComponent<ChaosSkillManager>();
                if (chaosManager == null)
                    chaosManager = GetComponentInParent<ChaosSkillManager>();

                if (chaosManager != null)
                    chaosManager.ApplyChaos(chosenSkill);
                else
                    Debug.LogWarning("[SkillManager] ChaosSkillManager 없음 — 혼돈 스킬 적용 실패");
                return;
            }

            // 진화 스킬인지 확인
            for (int i = 0; i < pendingEvolutions.Count; i++)
            {
                if (pendingEvolutions[i].evolvedSkillData.skillId == chosenSkill.skillId)
                {
                    TryExecuteEvolution(chosenSkill.skillId);
                    return;
                }
            }

            // 기존: 보유 중이면 레벨업, 아니면 신규
            if (HasSkill(chosenSkill.skillId))
                LevelUpSkill(chosenSkill.skillId);
            else
                AcquireSkill(chosenSkill);
        }

        // ===== 내부: 스킬 슬롯 생성 =====

        /// <summary>
        /// 스킬 오브젝트 생성 + 활성화.
        /// SkillManager의 자식으로 생성.
        /// [Step 4-6] SkillEffect 제거. Spawner를 Skill에 직접 전달.
        /// </summary>
        private Skill CreateSkillSlot(SkillData skillData)
        {
            GameObject slotObj;

            if (skillSlotPrefab != null)
            {
                slotObj = Instantiate(skillSlotPrefab, transform);
            }
            else
            {
                // 프리팹 없으면 빈 오브젝트 생성
                slotObj = new GameObject($"Skill_{skillData.skillName}");
                slotObj.transform.SetParent(transform);
                slotObj.AddComponent<Skill>();
            }

            slotObj.name = $"Skill_{skillData.skillName}";

            Skill skill = slotObj.GetComponent<Skill>();
            if (skill == null)
                skill = slotObj.AddComponent<Skill>();

            // [Step 4-6] Spawner 생성 (액티브 스킬만)
            ISkillSpawner spawner = null;
            ISkillSpawner phase2Spawner = null;
            if (skillData.skillType == SkillType.Active)
            {
                spawner = spawnerFactory.Create(skillData.effectType, skillData);

                // TwoPhase: Phase2 Spawner 생성 (장검 진화 등 — 궤도 후 투사체 발사)
                if (skillData.firingMode == FiringMode.TwoPhase
                    && skillData.projectilePrefab != null)
                {
                    phase2Spawner = new ProjectileSpawner(skillData.projectilePrefab);
                    phase2Spawner.Prewarm(skillData);
                }
            }

            skill.Activate(skillData, spawner, executorPrefab, phase2Spawner);

            // [Step 3-4] SkillTriggerSystem 초기화 (triggerEffects가 있는 경우)
            if (skillData.triggerEffects != null && skillData.triggerEffects.Count > 0)
            {
                var triggerSystem = slotObj.GetComponent<SkillTriggerSystem>();
                if (triggerSystem == null)
                    triggerSystem = slotObj.AddComponent<SkillTriggerSystem>();
                triggerSystem.Initialize(skillData.triggerEffects);
            }

            return skill;
        }

        // ===== [Step 4-6] SkillEffect 레이어 완전 제거 =====
        // Skill.Fire() → Executor → Spawner 직통.
        // 새 스킬 추가 시 ISkillSpawner 구현 + SkillSpawnerFactory.RegisterDefaults()에 등록.

        // ===== GameState 연동 =====

        /// <summary>
        /// 모든 스킬 일시정지 (레벨업 UI 표시 중).
        /// </summary>
        public void PauseAllSkills()
        {
            for (int i = 0; i < equippedSkills.Count; i++)
                equippedSkills[i].Deactivate();
        }

        /// <summary>
        /// 모든 스킬 재개.
        /// </summary>
        public void ResumeAllSkills()
        {
            for (int i = 0; i < equippedSkills.Count; i++)
            {
                var skill = equippedSkills[i];
                // [Phase 5 Fix] Activate 대신 Resume 사용 — Level 리셋 방지
                if (skill.Data.skillType == SkillType.Active)
                    skill.Resume();
            }
        }

        // ===== 외부 접근: 팩토리 =====

        /// <summary>
        /// 외부에서 런타임에 Spawner 타입을 추가 등록할 때 사용.
        /// </summary>
        public SkillSpawnerFactory SpawnerFactory => spawnerFactory;

        // ===== 디버그 =====

        public void LogSlotStatus()
        {
            Debug.Log($"[SkillManager] === 슬롯 상태 ({SlotCount}/{MaxSlots}) ===");
            for (int i = 0; i < equippedSkills.Count; i++)
            {
                var s = equippedSkills[i];
                string maxTag = s.IsMaxLevel ? " [MAX]" : "";
                Debug.Log($"  [{i}] {s.Data.skillName} Lv.{s.Level}/{s.Data.maxLevel} ({s.Data.skillType}){maxTag}");
            }
        }
    }
}