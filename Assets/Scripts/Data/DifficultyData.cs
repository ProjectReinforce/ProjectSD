using System;
using UnityEngine;

namespace SwDreams.Data
{
    /// <summary>
    /// 난이도 곡선 데이터. 시간대별 스폰 설정 + 인원수별 스케일링.
    /// enemy_boss_design.docx의 수치를 SO 에셋으로 관리.
    /// 
    /// 셋업:
    /// Assets/Data/ 폴더에서 Create → SwDreams → DifficultyData
    /// SpawnManager 인스펙터에서 연결.
    /// </summary>
    [CreateAssetMenu(fileName = "NewDifficultyData", menuName = "SwDreams/DifficultyData")]
    public class DifficultyData : ScriptableObject
    {
        [Header("시간대별 스폰 설정")]
        [Tooltip("시간순으로 정렬. 마지막 Phase가 게임 끝까지 유지됨.")]
        public SpawnPhase[] spawnPhases = new SpawnPhase[]
        {
            new SpawnPhase
            {
                phaseName = "워밍업",
                startTime = 0f, endTime = 60f,
                spawnInterval = 2.0f, maxEnemyCount = 15, healthMultiplier = 0.8f,
                spawnPerTick = 2,
                chaserRatio = 1.0f, runnerRatio = 0f, tankRatio = 0f, swarmRatio = 0f
            },
            new SpawnPhase
            {
                phaseName = "초반",
                startTime = 60f, endTime = 180f,
                spawnInterval = 1.5f, maxEnemyCount = 30, healthMultiplier = 1.0f,
                spawnPerTick = 2,
                chaserRatio = 0.80f, runnerRatio = 0.20f, tankRatio = 0f, swarmRatio = 0f
            },
            new SpawnPhase
            {
                phaseName = "중반 1",
                startTime = 180f, endTime = 360f,
                spawnInterval = 1.2f, maxEnemyCount = 50, healthMultiplier = 1.2f,
                spawnPerTick = 3,
                chaserRatio = 0.55f, runnerRatio = 0.20f, tankRatio = 0.10f, swarmRatio = 0.15f
            },
            new SpawnPhase
            {
                phaseName = "중반 2",
                startTime = 360f, endTime = 540f,
                spawnInterval = 1.0f, maxEnemyCount = 70, healthMultiplier = 1.5f,
                spawnPerTick = 3,
                chaserRatio = 0.45f, runnerRatio = 0.20f, tankRatio = 0.15f, swarmRatio = 0.20f
            },
            new SpawnPhase
            {
                phaseName = "후반",
                startTime = 540f, endTime = 780f,
                spawnInterval = 0.8f, maxEnemyCount = 90, healthMultiplier = 1.8f,
                spawnPerTick = 4,
                chaserRatio = 0.35f, runnerRatio = 0.25f, tankRatio = 0.15f, swarmRatio = 0.25f
            },
            new SpawnPhase
            {
                phaseName = "전초전",
                startTime = 780f, endTime = 900f,
                spawnInterval = 0.6f, maxEnemyCount = 120, healthMultiplier = 2.2f,
                spawnPerTick = 5,
                chaserRatio = 0.30f, runnerRatio = 0.25f, tankRatio = 0.15f, swarmRatio = 0.30f
            }
        };

        [Header("인원수별 스케일링")]
        [Tooltip("인덱스 0 = 1명, 인덱스 3 = 4명")]
        public PlayerScaling[] playerScalings = new PlayerScaling[]
        {
            new PlayerScaling { playerCount = 1, healthMultiplier = 0.6f, maxEnemyMultiplier = 0.6f, expMultiplier = 1.0f },
            new PlayerScaling { playerCount = 2, healthMultiplier = 1.0f, maxEnemyMultiplier = 1.0f, expMultiplier = 1.0f },
            new PlayerScaling { playerCount = 3, healthMultiplier = 1.4f, maxEnemyMultiplier = 1.3f, expMultiplier = 0.95f },
            new PlayerScaling { playerCount = 4, healthMultiplier = 1.8f, maxEnemyMultiplier = 1.6f, expMultiplier = 0.9f }
        };

        [Header("Swarm 설정")]
        [Tooltip("무리형 한 번에 스폰되는 수")]
        public int swarmGroupMin = 5;
        public int swarmGroupMax = 10;

        [Header("스폰 거리 (카메라 시야 기준)")]
        [Tooltip("카메라 시야 밖 최소 오프셋 (이 값만큼 화면 밖에서 스폰)")]
        public float spawnOffsetMin = 0.5f;
        [Tooltip("카메라 시야 밖 최대 오프셋")]
        public float spawnOffsetMax = 1.5f;

        [Tooltip("플레이어로부터 최소 이 거리 이상에서 스폰 (카메라 기반 fallback)")]
        public float playerSafeZone = 2.0f;
    }

    /// <summary>
    /// 시간대별 스폰 설정. 인스펙터에서 편집 가능.
    /// </summary>
    [Serializable]
    public struct SpawnPhase
    {
        public string phaseName;

        [Header("시간 범위 (초)")]
        public float startTime;
        public float endTime;

        [Header("스폰 설정")]
        [Tooltip("스폰 간격 (초)")]
        public float spawnInterval;

        [Tooltip("동시 활성 적 최대 수 (2인 기준)")]
        public int maxEnemyCount;

        [Tooltip("적 체력 배율 (2인 기준)")]
        public float healthMultiplier;

        [Tooltip("한 틱에 스폰하는 수 (Swarm 제외)")]
        [Range(1, 10)]
        public int spawnPerTick;

        [Header("적 타입 등장 비율 (합계 = 1.0)")]
        [Range(0f, 1f)] public float chaserRatio;
        [Range(0f, 1f)] public float runnerRatio;
        [Range(0f, 1f)] public float tankRatio;
        [Range(0f, 1f)] public float swarmRatio;
    }

    /// <summary>
    /// 인원수별 스케일링 설정.
    /// </summary>
    [Serializable]
    public struct PlayerScaling
    {
        public int playerCount;

        [Tooltip("적 체력에 곱해지는 배율")]
        public float healthMultiplier;

        [Tooltip("최대 동시 적 수에 곱해지는 배율")]
        public float maxEnemyMultiplier;

        [Tooltip("경험치에 곱해지는 배율")]
        public float expMultiplier;
    }
}