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
        // ===== 설정 =====
        public const int MaxSlots = 6;

        [Header("스킬 프리팹")]
        [SerializeField] private GameObject skillSlotPrefab;
        // 빈 오브젝트에 Skill 컴포넌트만 붙은 프리팹.
        // SkillEffect는 스킬 타입에 따라 동적 추가.

        // ===== 상태 =====
        private List<Skill> equippedSkills = new List<Skill>();

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
        private void CheckEvolution(Skill changedSkill)
        {
            if (!changedSkill.IsMaxLevel) return;

            SkillData data = changedSkill.Data;
            if (data.evolutionPair == null || data.evolvedSkill == null) return;

            // 짝이 되는 스킬도 보유 중이고 최대 레벨인지 체크
            Skill partner = GetSkill(data.evolutionPair.skillId);
            if (partner == null || !partner.IsMaxLevel) return;

            // 진화 발동!
            ExecuteEvolution(changedSkill, partner, data.evolvedSkill);
        }

        /// <summary>
        /// 진화 실행. 2슬롯 → 1슬롯 변환.
        /// </summary>
        private void ExecuteEvolution(Skill skillA, Skill skillB, SkillData evolvedData)
        {
            Debug.Log($"[SkillManager] ★ 진화! {skillA.Data.skillName} + {skillB.Data.skillName} → {evolvedData.skillName}");

            int idA = skillA.Data.skillId;
            int idB = skillB.Data.skillId;

            // 1) 기존 2개 제거
            RemoveSkill(idA);
            RemoveSkill(idB);

            // 2) 진화 스킬 추가 (슬롯 여유 확보됨: 2개 제거 → 1개 추가)
            Skill evolvedSkill = CreateSkillSlot(evolvedData);
            if (evolvedSkill != null)
            {
                equippedSkills.Add(evolvedSkill);
                OnSkillAdded?.Invoke(evolvedSkill);
            }

            // 3) 패시브 재계산 (패시브가 제거됐으므로)
            OnPassiveChanged?.Invoke();

            // 4) 진화 이벤트
            OnEvolution?.Invoke(evolvedData);
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
        /// 레벨업 시 표시할 선택지 3개 생성.
        /// 호스트에서 각 플레이어의 SkillManager 상태를 참조하여 호출.
        /// </summary>
        /// <param name="allSkills">전체 스킬 풀 (SkillDatabase에서 제공)</param>
        /// <param name="count">선택지 수 (기본 3)</param>
        /// <returns>선택지 SkillData 배열. 이미 보유 중이면 "레벨업" 의미.</returns>
        public SkillData[] GenerateChoices(SkillData[] allSkills, int count = 3)
        {
            List<SkillData> candidates = new List<SkillData>();

            if (HasEmptySlot)
            {
                // 슬롯 여유 있음 → 전체 풀에서 후보 수집
                for (int i = 0; i < allSkills.Length; i++)
                {
                    SkillData sd = allSkills[i];

                    // 혼돈 스킬은 별도 시스템에서 처리
                    if (sd.skillType == SkillType.Chaos) continue;

                    // 이미 보유 + 최대 레벨이면 제외
                    Skill existing = GetSkill(sd.skillId);
                    if (existing != null && existing.IsMaxLevel) continue;

                    candidates.Add(sd);
                }
            }
            else
            {
                // 슬롯 꽉 참 → 보유 중이면서 레벨업 가능한 것만
                for (int i = 0; i < equippedSkills.Count; i++)
                {
                    if (!equippedSkills[i].IsMaxLevel)
                        candidates.Add(equippedSkills[i].Data);
                }
            }

            // 후보가 요청 수보다 적을 수 있음
            int resultCount = Mathf.Min(count, candidates.Count);
            SkillData[] result = new SkillData[resultCount];

            // Fisher-Yates 셔플로 랜덤 선택
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
        public bool ApplyChoice(SkillData chosenSkill)
        {
            if (chosenSkill == null) return false;

            if (HasSkill(chosenSkill.skillId))
            {
                // 이미 보유 → 레벨업
                return LevelUpSkill(chosenSkill.skillId);
            }
            else
            {
                // 새 스킬 획득
                return AcquireSkill(chosenSkill);
            }
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
                case SkillEffectType.Projectile:
                    return slotObj.AddComponent<ProjectileEffect>();
                    // TODO Phase 5: projectilePrefab 연결 필요
                    // ProjectileEffect에 SetPrefab() 메서드 추가하거나
                    // SkillData에 projectilePrefab 필드 추가

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