using UnityEngine;

namespace SwDreams.Shared.Data
{
    /// <summary>
    /// 적 사망 시 어떤 픽업을 어떤 확률로 드랍할지 정의하는 SO.
    ///
    /// EnemyData 에서 참조. DropSpawner 가 적 사망 이벤트에서 이 테이블을 조회해
    /// 확률 롤 + 등급 롤을 수행한 뒤 배치 큐에 적재.
    ///
    /// StatBoost 는 월드 드랍 대상이 아님(만렙 레벨업/퀘스트 보상) — 제외.
    /// </summary>
    [CreateAssetMenu(fileName = "NewEnemyDropTable", menuName = "SwDreams/Data/EnemyDropTable")]
    public class EnemyDropTable : ScriptableObject
    {
        [Header("드랍 확률 (0~1)")]
        [Tooltip("엘리트 전용. 일반 적에게 설정해도 DropSpawner 가 무시.")]
        [Range(0f, 1f)] public float essenceChance = 0f;

        [Tooltip("무기 드랍 확률. 매우 낮게 권장 (0.01 수준).")]
        [Range(0f, 1f)] public float weaponChance = 0f;

        [Tooltip("자석 드랍 확률.")]
        [Range(0f, 1f)] public float magnetChance = 0f;

        [Tooltip("물약 드랍 확률.")]
        [Range(0f, 1f)] public float potionChance = 0f;

        [Header("등급 가중치 (Rarity enum 순서: Common/Rare/Epic/Legendary)")]
        [Tooltip("정수 드랍 시 등급 가중치. 배열 길이 4 권장.")]
        public float[] essenceRarityWeights = { 60f, 25f, 12f, 3f };

        [Tooltip("무기 드랍 시 등급 가중치. 배열 길이 4 권장.")]
        public float[] weaponRarityWeights = { 60f, 25f, 12f, 3f };
    }
}
