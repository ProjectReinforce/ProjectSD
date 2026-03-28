using System.Collections.Generic;
using UnityEngine;
using SwDreams.Domain.ValueObjects;

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

    /// <summary>
    /// 스킬 데이터 base 클래스. 모든 스킬 타입의 공통 필드 포함.
    /// SO 생성은 서브클래스(ProjectileSkillData 등)의 CreateAssetMenu를 사용.
    /// </summary>
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

        [Header("발사 모드 (Executor)")]
        [Tooltip("Executor 발사 패턴. Simultaneous=동시, DelayedBurst=시간차, TwoPhase=2단계, Single=1개")]
        public FiringMode firingMode = FiringMode.SimultaneousSpread;
        [Tooltip("DelayedBurst 모드에서 각 발사 간 딜레이 (초)")]
        public float burstDelay = 0.1f;

        [Header("패시브 전용")]
        public PassiveBonusType bonusType;
        public float bonusPerLevel = 0f;

        [Header("투사체 전용")]
        public GameObject projectilePrefab;
        public float projectileSpeed = 5f;
        public int projectileCount = 1;
        public float projectileLifetime = 5f;
        [Tooltip("적 적중 시 관통 여부. true면 소멸하지 않음.")]
        public bool penetrates = false;
        [Tooltip("적중/소멸 시 생성할 서브 투사체 프리팹 (분기탄 등). null이면 미사용.")]
        public GameObject subProjectilePrefab;

        [Header("투사체 배치/궤적")]
        [Tooltip("발사 기준 방향")]
        public AimType aimType = AimType.ClosestEnemy;
        [Tooltip("다중 투사체 배치 패턴")]
        public SpreadPatternType spreadPattern = SpreadPatternType.Fan;
        [Tooltip("부채꼴 배치 시 개별 각도 (도)")]
        public float spreadAngle = 15f;
        [Tooltip("투사체 궤적 패턴")]
        public TrajectoryType trajectoryType = TrajectoryType.Straight;
        [Tooltip("파형 궤적(Zigzag/SinWave)의 진폭")]
        public float waveAmplitude = 0.8f;
        [Tooltip("파형 궤적(Zigzag/SinWave)의 주파수")]
        public float waveFrequency = 5f;

        [Header("투사체 — 유도/왕복/회오리 파라미터")]
        [Tooltip("유도 회전 속도 (도/초). 높을수록 급선회")]
        public float homingRotateSpeed = 300f;
        [Tooltip("체인 비행 횟수. 0이면 비활성. 적중 시 소멸 대신 타겟 교체.")]
        public int chainFlightCount = 0;
        [Tooltip("체인 비행 시 다음 타겟 탐색 반경")]
        public float chainSearchRadius = 5f;
        [Tooltip("회오리 끌어당김 반경")]
        public float pullRadius = 2f;
        [Tooltip("회오리 끌어당김 힘")]
        public float pullForce = 3f;
        [Tooltip("복귀 경로 끌어당김 (그래비톤 부메랑)")]
        public bool hasPullOnReturn = false;
        [Tooltip("나선 확장 속도 (대선풍)")]
        public float spiralExpandSpeed = 1f;

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
        public float randomSpawnRadius = 3f;

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

        [Header("패시브 적용 필터")]
        [Tooltip("이 스킬에 영향을 주는 스탯 목록. 비어있으면 전부 적용.")]
        public List<StatType> applicableStats = new List<StatType>();

        [Header("Trigger+Effect 조합")]
        [Tooltip("기본 추가 효과. 진화 스킬 등에서 설정. 런타임 추가는 SkillTriggerSystem 사용.")]
        public List<SkillTriggerEffect> triggerEffects = new List<SkillTriggerEffect>();

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

        /// <summary>
        /// 이 스킬에 해당 스탯이 영향을 주는지 확인.
        /// applicableStats가 비어있으면 모든 스탯 적용 (하위 호환).
        /// </summary>
        public bool IsStatApplicable(StatType statType)
        {
            if (applicableStats == null || applicableStats.Count == 0)
                return true;
            return applicableStats.Contains(statType);
        }
    }
}