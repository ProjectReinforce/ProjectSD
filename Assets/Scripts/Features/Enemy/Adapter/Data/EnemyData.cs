using UnityEngine;
using SwDreams.Features.Enemy.Adapter.Data;
using SwDreams.Shared.Data;

namespace SwDreams.Features.Enemy.Adapter.Data
{
    public enum EnemyType
    {
        Chaser,  // 기본 추적형
        Runner,  // 빠른형 (Phase 3)
        Tank,    // 둔한형 (Phase 3)
        Swarm,   // 무리형 (Phase 3)
        Ranged   // 원거리형 (Phase B)
    }

    public enum RangedBehavior
    {
        Stationary, // 고정형 — 위치 이동 없음
        Kite        // 추격형 — 사거리 안까지 접근
    }

    public enum RangedAttack
    {
        Projectile, // 투사체 발사 (회피 가능한 속도)
        Telegraph   // 경고 비주얼 → 지연 폭발
    }

    [CreateAssetMenu(fileName = "NewEnemyData", menuName = "SwDreams/EnemyData")]
    public class EnemyData : ScriptableObject
    {
        [Header("기본 정보")]
        public string enemyName;
        public EnemyType enemyType;
        public Sprite sprite;

        [Header("스탯")]
        public int baseHP = 30;
        public float moveSpeed = 0.48f;
        public int contactDamage = 10;

        [Header("보상")]
        public int expValue = 5;

        [Header("비주얼")]
        [Tooltip("스폰 시 Enemy 의 localScale 에 곱할 배율. 프리팹 기본=1.\n" +
                 "엘리트는 1.3~1.5 로 커 보이게, 작은 적은 0.8 등으로 축소 가능.")]
        public float visualScaleMultiplier = 1f;

        [Header("특수 (Phase 3)")]
        [Range(0f, 1f)]
        public float knockbackResistance = 0f;

        [Header("이동 제약")]
        // 겹침 해소(Anti-Overlap) 사용 여부. Swarm은 밀집 돌진이 컨셉이라 false.
        public bool resolveOverlap = true;

        [Header("원거리 (EnemyType.Ranged 전용)")]
        public RangedBehavior rangedBehavior;
        public RangedAttack rangedAttack;
        public float attackRange = 7f;
        public float attackInterval = 3f;
        public int attackDamage = 20;

        [Header("  └ Projectile 공격")]
        public GameObject projectilePrefab;
        public float projectileSpeed = 7f;
        public float projectileLifetime = 2f;

        [Header("  └ Telegraph 공격")]
        public GameObject telegraphPrefab;
        public float telegraphDuration = 1.0f;
        public float telegraphRadius = 1.5f;

        [Header("엘리트 (Phase C)")]
        [Tooltip("true: 엘리트 취급. SpawnManager 의 eliteVariants 배열에 등록해서 독립 타이머로 스폰.\n" +
                 "체력/데미지 배율은 이 SO 의 baseHP/contactDamage 에 직접 반영(일반 대비 ×4~6 권장).")]
        public bool isElite = false;

        [Tooltip("엘리트 사망 시 정수(Essence) 드랍 확률 0~1. 실제 Essence 시스템은 별도 구현 — 현재는 훅 로그만.\n" +
                 "[Phase 2 예정] 아래 dropTable 로 완전 이관되면 제거.")]
        [Range(0f, 1f)]
        public float essenceDropChance = 0f;

        [Header("드랍 (Phase 0 인프라)")]
        [Tooltip("정수/무기/자석/물약 드랍 확률 + 등급 가중치. null 이면 DropSpawner 가 아무것도 안 떨어뜨림.\n" +
                 "엘리트 전용 정수 드랍은 이 SO 의 essenceChance 와 isElite=true 둘 다 만족해야 발동.")]
        public EnemyDropTable dropTable;
    }
}