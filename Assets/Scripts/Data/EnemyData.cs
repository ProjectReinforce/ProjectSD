using UnityEngine;

namespace SwDreams.Data
{
    public enum EnemyType
    {
        Chaser,  // 기본 추적형
        Runner,  // 빠른형 (Phase 3)
        Tank,    // 둔한형 (Phase 3)
        Swarm    // 무리형 (Phase 3)
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
        public float moveSpeed = 3f;
        public int contactDamage = 10;

        [Header("보상")]
        public int expValue = 5;

        [Header("특수 (Phase 3)")]
        [Range(0f, 1f)]
        public float knockbackResistance = 0f;
    }
}
