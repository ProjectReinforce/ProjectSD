using UnityEngine;

namespace SwDreams.Data
{
    /// <summary>
    /// 전체 스킬 풀을 관리하는 ScriptableObject.
    /// SkillManager.GenerateChoices()에 전달할 스킬 목록 제공.
    ///
    /// 셋업:
    /// Assets/Data/ 폴더에서 Create → SwDreams → SkillDatabase
    /// 인스펙터에서 모든 SkillData SO를 연결.
    ///
    /// 사용:
    /// - LevelUpManager에서 참조
    /// - activeSkills + passiveSkills → 일반 레벨업 선택지 풀
    /// - chaosSkills → Lv.5/10/15 혼돈 스킬 선택지 풀
    /// </summary>
    [CreateAssetMenu(fileName = "SkillDatabase", menuName = "SwDreams/SkillDatabase")]
    public class SkillDatabase : ScriptableObject
    {
        [Header("액티브 스킬 (10종)")]
        public SkillData[] activeSkills;

        [Header("패시브 스킬 (13종)")]
        public SkillData[] passiveSkills;

        [Header("혼돈 스킬 (6종)")]
        public SkillData[] chaosSkills;

        [Header("진화 스킬 (참조용 — EvolutionPair는 각 SkillData에서 설정)")]
        public SkillData[] evolvedSkills;

        /// <summary>
        /// 일반 레벨업용 풀 (액티브 + 패시브 합침).
        /// 캐싱해서 매번 새 배열 안 만들어도 되지만,
        /// 레벨업은 드물게 발생하므로 GC 부담 미미.
        /// </summary>
        public SkillData[] GetNormalPool()
        {
            int totalCount = 0;
            if (activeSkills != null) totalCount += activeSkills.Length;
            if (passiveSkills != null) totalCount += passiveSkills.Length;

            SkillData[] pool = new SkillData[totalCount];
            int idx = 0;

            if (activeSkills != null)
            {
                for (int i = 0; i < activeSkills.Length; i++)
                    pool[idx++] = activeSkills[i];
            }

            if (passiveSkills != null)
            {
                for (int i = 0; i < passiveSkills.Length; i++)
                    pool[idx++] = passiveSkills[i];
            }

            return pool;
        }

        /// <summary>
        /// 스킬 ID로 SkillData 검색. 전체 풀에서 탐색.
        /// RPC로 받은 스킬 ID를 SkillData로 변환할 때 사용.
        /// </summary>
        public SkillData GetSkillById(int skillId)
        {
            // 액티브
            if (activeSkills != null)
            {
                for (int i = 0; i < activeSkills.Length; i++)
                {
                    if (activeSkills[i] != null && activeSkills[i].skillId == skillId)
                        return activeSkills[i];
                }
            }

            // 패시브
            if (passiveSkills != null)
            {
                for (int i = 0; i < passiveSkills.Length; i++)
                {
                    if (passiveSkills[i] != null && passiveSkills[i].skillId == skillId)
                        return passiveSkills[i];
                }
            }

            // 혼돈
            if (chaosSkills != null)
            {
                for (int i = 0; i < chaosSkills.Length; i++)
                {
                    if (chaosSkills[i] != null && chaosSkills[i].skillId == skillId)
                        return chaosSkills[i];
                }
            }

            // 진화
            if (evolvedSkills != null)
            {
                for (int i = 0; i < evolvedSkills.Length; i++)
                {
                    if (evolvedSkills[i] != null && evolvedSkills[i].skillId == skillId)
                        return evolvedSkills[i];
                }
            }

            Debug.LogWarning($"[SkillDatabase] 스킬 ID {skillId} 찾기 실패");
            return null;
        }

        // ===== 에디터 검증 =====
        private void OnValidate()
        {
            ValidateNoDuplicateIds();
        }

        private void ValidateNoDuplicateIds()
        {
            var allArrays = new SkillData[][] { activeSkills, passiveSkills, chaosSkills, evolvedSkills };
            var ids = new System.Collections.Generic.Dictionary<int, string>();

            foreach (var array in allArrays)
            {
                if (array == null) continue;
                foreach (var skill in array)
                {
                    if (skill == null) continue;
                    if (ids.ContainsKey(skill.skillId))
                    {
                        // Error → Warning으로 변경 (에디터 편집 중 오작동 방지)
                        Debug.LogWarning($"[SkillDatabase] 중복 스킬 ID: {skill.skillId}" +
                            $" ({skill.skillName}) ↔ ({ids[skill.skillId]})");
                    }
                    else
                    {
                        ids[skill.skillId] = skill.skillName;
                    }
                }
            }
        }
    }
}