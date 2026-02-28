using System;
using System.Collections.Generic;
using UnityEngine;
using SwDreams.Data;

namespace SwDreams.Adapter.Skill
{
    /// <summary>
    /// 플레이어 스킬 6슬롯 관리.
    /// 각 플레이어의 자식 오브젝트에 부착.
    ///
    /// 역할:
    /// - 스킬 획득/레벨업/제거
    /// - 6슬롯 제한 관리 (액티브 + 패시브 합계)
    /// - 진화 가능 여부 감지
    /// - 선택지 생성 (호스트용)
    /// - 패시브 스킬 → PlayerStats 반영
    ///
    /// 프리팹 구성:
    /// Player(또는 PlayerStub)의 자식에 빈 오브젝트 "Skills"
    /// → SkillManager 부착
    /// → 스킬 획득 시 자식으로 Skill 오브젝트 동적 생성
    ///
    /// 네트워크:
    /// 호스트가 선택지 생성 + 결과 적용을 관리.
    /// SkillManager 자체는 PhotonView 불필요 (LevelUpManager가 RPC 처리).
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
        public const int MaxSlots = 6;

        [Header("스킬 프리팹")]
        [SerializeField] private GameObject skillSlotPrefab;
        // 빈 오브젝트에 Skill 컴포넌트만 붙은 프리팹.
        // SkillEffect는 스킬 타입에 따라 동적 추가.

        // ===== 상태 =====
        private List<Skill> equippedSkills = new List<Skill>();
        private List<EvolutionCandidate> pendingEvolutions = new List<EvolutionCandidate>();

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

        /// <summary>패시브 변경 시 발생. PlayerStats 재계산용.</summary>
        public event Action OnPassiveChanged;

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

            // 패시브면 스탯 재계산 트리거
            if (skillData.skillType == SkillType.Passive)
                OnPassiveChanged?.Invoke();

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

            // 패시브면 스탯 재계산
            if (skill.Data.skillType == SkillType.Passive)
                OnPassiveChanged?.Invoke();

            // 진화 가능 체크
            CheckEvolution(skill);

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

            // 기존 2개 스킬 제거
            RemoveSkill(evo.activeSkillId);
            RemoveSkill(evo.passiveSkillId);

            // 진화 스킬 생성
            CreateSkillSlot(evo.evolvedSkillData);

            // 대기열에서 제거
            pendingEvolutions.RemoveAt(targetIndex);

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
        /// <param name="count">선택지 개수 (기본 3)</param>
        /// <param name="evolutionChance">진화 등장 확률 (0~1, 기본 0.7)</param>
        public SkillData[] GenerateChoices(SkillData[] pool, int count = 3, float evolutionChance = 0.7f)
        {
            // 1) 진화 후보 수집
            SkillData evolutionChoice = null;
            if (pendingEvolutions.Count > 0 && UnityEngine.Random.value < evolutionChance)
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
        /// <param name="count">선택지 수 (기본 3)</param>
        public SkillData[] GenerateChaosChoices(SkillData[] chaosSkills, int count = 3)
        {
            // 혼돈 스킬은 슬롯을 차지하지 않으므로 단순 랜덤
            List<SkillData> candidates = new List<SkillData>(chaosSkills);

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
        /// <returns>true: 성공</returns>
        public void ApplyChoice(SkillData chosenSkill)
        {
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
        /// SkillEffect는 SkillData의 effectType에 따라 동적 추가.
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

            // SkillEffect 동적 추가 (액티브 스킬만)
            SkillEffect effect = null;
            if (skillData.skillType == SkillType.Active)
            {
                effect = AddSkillEffect(slotObj, skillData);
            }

            skill.Activate(skillData, effect);
            return skill;
        }

        /// <summary>
        /// SkillData의 effectType에 따라 적절한 SkillEffect 컴포넌트 추가.
        /// Phase 4: ProjectileEffect만. Phase 5에서 나머지 추가.
        /// </summary>
        private SkillEffect AddSkillEffect(GameObject slotObj, SkillData skillData)
        {
            switch (skillData.effectType)
            {
                case SkillEffectType.None:
                    return null;
                case SkillEffectType.Projectile:
                    var projEffect = slotObj.AddComponent<ProjectileEffect>();
                    if (skillData.projectilePrefab != null)
                        projEffect.SetProjectilePrefab(skillData.projectilePrefab);
                    else
                        Debug.LogWarning($"[SkillManager] {skillData.skillName}: projectilePrefab 미설정!");
                    return projEffect;

                // Phase 5 확장 지점:
                // case SkillEffectType.Area:
                //     return slotObj.AddComponent<AreaEffect>();
                // case SkillEffectType.Orbital:
                //     return slotObj.AddComponent<OrbitalEffect>();
                // case SkillEffectType.Placed:
                //     return slotObj.AddComponent<PlacedEffect>();
                // case SkillEffectType.Debuff:
                //     return slotObj.AddComponent<DebuffEffect>();

                default:
                    Debug.LogWarning($"[SkillManager] 미구현 SkillEffectType: {skillData.effectType}");
                    return null;
            }
        }

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
                // 패시브는 Activate 불필요 (수치 보정만)
                if (skill.Data.skillType == SkillType.Active)
                    skill.Activate(skill.Data);
            }
        }

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