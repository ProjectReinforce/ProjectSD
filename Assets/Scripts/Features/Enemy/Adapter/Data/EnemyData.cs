using UnityEngine;
using SwDreams.Features.Enemy.Adapter.Data;

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
    }
}