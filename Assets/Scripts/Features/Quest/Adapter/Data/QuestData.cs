using SwDreams.Features.Enemy.Adapter.Data;
using SwDreams.Features.Quest.Domain;
using UnityEngine;

namespace SwDreams.Features.Quest.Adapter.Data
{
    /// <summary>
    /// 퀘스트 데이터. SO. 거점 프리팹은 별도(QuestZone), 본 SO 는 진행 룰만 정의.
    /// 보상은 StatBoost 선택지 (LevelUpManager 경유).
    /// </summary>
    [CreateAssetMenu(menuName = "SwDreams/Data/QuestData")]
    public class QuestData : ScriptableObject
    {
        [Header("기본 정보")]
        public int questId;
        public string displayName;
        [TextArea] public string description;

        [Header("진행")]
        public QuestType questType = QuestType.KillTarget;

        [Tooltip("거점 진입 반경 (모든 플레이어가 들어와야 시작 카운트다운 진입)")]
        public float triggerRadius = 3f;

        [Tooltip("진입 후 시작까지 대기 시간(초). 도중 이탈 시 리셋.")]
        public float waitTime = 3f;

        [Tooltip("퀘스트 제한 시간(초). 0 이하면 무제한.")]
        public float timeLimit = 0f;

        [Tooltip("KillTarget/KillInTime/DodgeFalling 의 목표 횟수.")]
        public int targetCount = 3;

        [Header("격리")]
        [Tooltip("격리 몹 EnemyData. null 이면 격리 비활성.")]
        public EnemyData barrierEnemyData;

        [Tooltip("격리 몹 개수 (구역 둘레 분배).")]
        public int barrierEnemyCount = 8;

        [Tooltip("거점 중심에서 격리 몹 배치 반경.")]
        public float barrierRadius = 5f;

        [Header("보상")]
        [Tooltip("4등급 가중치 (Common/Rare/Epic/Legendary). 비어 있으면 GameplayConfig.defaultRarityWeights 사용.")]
        public float[] rewardRarityWeights = new float[0];
    }
}
