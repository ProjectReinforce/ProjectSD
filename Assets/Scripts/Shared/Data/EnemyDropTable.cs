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
        [Header("드랍 확률 (0~1, 각 종류 독립 롤)")]
        [Tooltip("정수(Essence) 드랍 확률. 엘리트 전용 — DropSpawner 가 isElite=false 면 무시.\n" +
                 "일반 적 SO 에 값을 설정해도 드랍되지 않음. 관리 일관성 차원에서 공용 필드.")]
        [Range(0f, 1f)] public float essenceChance = 0f;

        [Tooltip("무기 드랍 확률. 매우 낮게 권장 (0.01 수준).")]
        [Range(0f, 1f)] public float weaponChance = 0f;

        [Tooltip("자석 드랍 확률.")]
        [Range(0f, 1f)] public float magnetChance = 0f;

        [Tooltip("물약 드랍 확률.")]
        [Range(0f, 1f)] public float potionChance = 0f;

        [Header("정수 속성 가중치 (EssenceType 순서: Ice / Fire / Lightning)")]
        [Tooltip("정수 드랍 시 어느 속성이 떨어질지 결정. 배열 길이 3 권장. 정수는 등급 체계 없음.")]
        public float[] essenceTypeWeights = { 1f, 1f, 1f };

        [Header("무기 등급 가중치 (Rarity 순서: Common/Rare/Epic/Legendary)")]
        [Tooltip("무기 드랍 시 등급 가중치. 배열 길이 4 권장.")]
        public float[] weaponRarityWeights = { 60f, 25f, 12f, 3f };
    }
}
