using UnityEngine;

namespace SwDreams.Data
{
    public enum SkillType
    {
        Active,
        Passive,
        Chaos
    }

    public enum SkillEffectType
    {
        Projectile,       // 표창, 매직미사일, 부메랑, 회오리바람
        Area,             // 번개, 개미지옥, 성역
        Orbital,          // 장검
        Placed,           // 자동포탑
        Debuff            // 저주인형
    }

    [CreateAssetMenu(fileName = "NewSkillData", menuName = "SwDreams/SkillData")]
    public class SkillData : ScriptableObject
    {
        [Header("기본 정보")]
        public int skillId;
        public string skillName;
        public SkillType skillType;
        public SkillEffectType effectType;
        
        [Header("UI 표시용")]
        public Sprite icon;
        [TextArea] public string description;

        [Header("레벨 스케일링")]
        public int maxLevel = 7;
        public int[] damagePerLevel = { 15, 18, 22, 26, 31, 37, 45 };
        public float[] cooldownPerLevel = { 1.5f, 1.4f, 1.3f, 1.2f, 1.1f, 1.0f, 0.9f };

        [Header("투사체 전용")]
        public GameObject projectilePrefab;
        public float projectileSpeed = 10f;
        public int projectileCount = 1;
        public float projectileLifetime = 5f;

        [Header("범위 전용")]
        public float areaRadius = 2f;
        public float areaDuration = 3f;

        [Header("진화 연결 (Phase 4)")]
        public SkillData evolutionPair;    // 이 스킬과 조합되는 패시브/액티브
        public SkillData evolvedSkill;     // 진화 결과 스킬

        public int GetDamageForLevel(int level)
        {
            int index = Mathf.Clamp(level - 1, 0, damagePerLevel.Length - 1);
            return damagePerLevel[index];
        }

        public float GetCooldownForLevel(int level)
        {
            int index = Mathf.Clamp(level - 1, 0, cooldownPerLevel.Length - 1);
            return cooldownPerLevel[index];
        }
    }
}
