using System.Collections.Generic;
using UnityEngine;

namespace Features.Skill.Bootstrap
{
    public sealed class SkillCatalog
    {
        private readonly Dictionary<string, SkillData> _dataById = new Dictionary<string, SkillData>();
        private readonly SkillData[] _allSkills;

        public SkillCatalog(SkillCatalogData catalogData)
        {
            _allSkills = catalogData.Skills;
            foreach (var data in _allSkills)
            {
                if (data == null) continue;
                if (_dataById.ContainsKey(data.SkillId))
                {
                    Debug.LogWarning($"[SkillCatalog] Duplicate skill ID: {data.SkillId}");
                    continue;
                }
                _dataById[data.SkillId] = data;
            }
        }

        public Domain.Skill Get(string skillId)
        {
            if (_dataById.TryGetValue(skillId, out var data))
                return data.ToDomain();

            Debug.LogError($"[SkillCatalog] Skill not found: {skillId}");
            return null;
        }

        public SkillData GetData(string skillId)
        {
            _dataById.TryGetValue(skillId, out var data);
            return data;
        }

        public SkillData[] AllSkills => _allSkills;
    }
}
