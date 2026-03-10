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
        None,
        Projectile,       // 표창, 매직미사일, 부메랑, 회오리바람
        Area,             // 번개, 개미지옥, 성역
        Orbital,          // 장검
        Placed,           // 자동포탑
        Debuff            // 저주인형
    }

    public enum ChaosEffectType
    {
        None,
        GlassCannon,      // 유리대포: 최대 체력 절반, 공격력 2배
        ChainExplosion,   // 연쇄 폭발: 적 처치 시 주변 폭발
        BerserkMode,      // 폭주 모드: HP 30% 이하 시 CDR 절반 + 이속 50% 증가
        AccelEngine,      // 가속 엔진: 시간 경과에 따라 스탯 증가
        Unity,            // 단결: 팀원 밀집 시 데미지 증폭
        Gambler           // 도박꾼: 선택지 1개가 한 등급 높게 등장
    }

    public enum PassiveBonusType
    {
        None,               // 액티브/혼돈 스킬
        ProjectileSpeed,    // 투사체 속도
        ProjectileCount,    // 투사체 개수
        SkillRange,         // 스킬 범위
        SkillDuration,      // 스킬 유지 시간
        AttackMultiplier,   // 공격력 배율 (0.1 = +10%)
        Knockback,          // 넉백
        HealingMultiplier,  // 회복량
        CritDamage,         // 치명타 데미지
        CooldownReduction,  // 쿨타임 감소 (0.04 = 4%)
        MaxHP,              // 최대 체력
        MoveSpeed,          // 이동속도
        Defense,            // 방어력 (0.05 = 5%)
        ExpMultiplier       // 경험치 배율 (0.1 = +10%)
    }

    [CreateAssetMenu(fileName = "NewSkillData", menuName = "SwDreams/SkillData")]
    public class SkillData : ScriptableObject
    {
        [Header("기본 정보")]
        public int skillId;
        public string skillName;
        public SkillType skillType;
        public SkillEffectType effectType;
        public ChaosEffectType chaosEffectType;
        
        [Header("UI 표시용")]
        public Sprite icon;
        [TextArea] public string description;

        [Header("레벨 스케일링")]
        public int maxLevel = 7;
        public int[] damagePerLevel = { 15, 18, 22, 26, 31, 37, 45 };
        public float[] cooldownPerLevel = { 1.5f, 1.4f, 1.3f, 1.2f, 1.1f, 1.0f, 0.9f };

        [Header("패시브 전용")]
        public PassiveBonusType bonusType;
        public float bonusPerLevel = 0f;

        [Header("투사체 전용")]
        public GameObject projectilePrefab;
        public float projectileSpeed = 10f;
        public int projectileCount = 1;
        public float projectileLifetime = 5f;
        [Tooltip("유도 투사체 (매직 미사일)")]
        public bool isHoming = false;
        [Tooltip("유도 회전 속도 (도/초). 높을수록 급선회")]
        public float homingRotateSpeed = 300f;
        [Tooltip("왕복 투사체 (부메랑)")]
        public bool isBoomerang = false;
        [Tooltip("느린 전진 + 범위 끌어당김 (회오리바람)")]
        public bool isTornado = false;
        [Tooltip("회오리 끌어당김 반경")]
        public float pullRadius = 2f;
        [Tooltip("회오리 끌어당김 힘")]
        public float pullForce = 3f;

        [Header("진화 전용 — 투사체")]
        [Tooltip("적중 시 폭발 (폭렬 표창)")]
        public bool isExploding = false;
        [Tooltip("폭발 반경")]
        public float explosionRadius = 1.5f;
        [Tooltip("적중 후 체인 (체인 미사일). 최대 체인 횟수")]
        public int chainCount = 0;
        [Tooltip("체인 탐색 반경")]
        public float chainRadius = 4f;
        [Tooltip("복귀 경로 끌어당김 (그래비톤 부메랑)")]
        public bool hasPullOnReturn = false;
        [Tooltip("나선형 이동 (대선풍)")]
        public bool isSpiral = false;
        [Tooltip("나선 확장 속도")]
        public float spiralExpandSpeed = 1f;

        [Header("진화 전용 — 장판")]
        [Tooltip("범위 내 적 슬로우 (뇌전역)")]
        public bool appliesSlow = false;
        [Tooltip("슬로우 배율 (0.5 = 50% 감속)")]
        public float slowMultiplier = 0.5f;
        [Tooltip("HP 비율 이하 적 즉사 (나락). 0이면 비활성")]
        [Range(0f, 1f)]
        public float executeThreshold = 0f;
        [Tooltip("회복+데미지 동시 (심판의 성역)")]
        public bool isDualZone = false;

        [Header("범위/장판 전용 (Area)")]
        public float areaRadius = 2f;
        public float areaDuration = 3f;
        [Tooltip("장판 틱 간격 (초). 짧을수록 자주 판정")]
        public float tickRate = 0.5f;
        [Tooltip("true = 회복 장판 (성역), false = 피해 장판 (개미지옥)")]
        public bool isHealingEffect = false;
        [Tooltip("true = 플레이어 위치가 아닌 랜덤 위치에 생성 (번개)")]
        public bool spawnAtRandomPosition = false;
        [Tooltip("랜덤 생성 반경 (플레이어 기준)")]
        public float randomSpawnRadius = 5f;

        [Header("회전형 전용 (Orbital)")]
        [Tooltip("궤도 반경 (플레이어 중심 거리)")]
        public float orbitRadius = 1.5f;
        [Tooltip("초당 회전 각도")]
        public float rotationSpeed = 180f;
        [Tooltip("회전 오브젝트 개수")]
        public int objectCount = 3;
        [Tooltip("넉백 힘")]
        public float knockbackForce = 2f;

        [Header("설치형 전용 (Placed)")]
        [Tooltip("포탑 공격 사거리")]
        public float attackRange = 5f;
        [Tooltip("포탑 공격 간격 (초)")]
        public float attackCooldown = 0.5f;
        [Tooltip("항상 치명타")]
        public bool alwaysCritical = false;

        [Header("디버프 전용 (Debuff)")]
        [Tooltip("디버프 지속시간 (초)")]
        public float debuffDuration = 5f;
        [Tooltip("받는 피해 증가 배율 (1.3 = +30%)")]
        public float damageAmplify = 1.3f;
        [Tooltip("동시 디버프 대상 수")]
        public int targetCount = 3;
        [Tooltip("사망 시 가까운 적에게 전이 (역병 인형). 전이 수")]
        public int spreadOnDeathCount = 0;

        [Header("공통 효과")]
        [Tooltip("최대 동시 설치/장판 수")]
        public int maxInstances = 3;
        [Tooltip("효과 프리팹 (장판/회전체/포탑/마커)")]
        public GameObject effectPrefab;

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
