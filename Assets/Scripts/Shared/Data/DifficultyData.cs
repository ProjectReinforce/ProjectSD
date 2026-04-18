using System;
using UnityEngine;

namespace SwDreams.Shared.Data
{
    /// <summary>
    /// 난이도 곡선 데이터. AnimationCurve 기반.
    /// Inspector에서 그래프를 직접 드래그하여 곡선 조절 가능.
    ///
    /// 모든 곡선의 X축 = 0~1 (게임 시작~보스 등장).
    /// Y축 = 0~1 (start~end 사이의 보간 비율).
    ///
    /// 실제 값 = start + (end - start) × curve.Evaluate(t)
    ///
    /// 셋업:
    /// Assets/Data/ 폴더에서 Create > SwDreams > DifficultyData
    /// SpawnManager 인스펙터에서 연결.
    /// Inspector에서 각 곡선 클릭 → 커브 에디터에서 S자, J자, 선형 등 자유롭게 조절.
    /// </summary>
    [CreateAssetMenu(fileName = "NewDifficultyData", menuName = "SwDreams/DifficultyData")]
    public class DifficultyData : ScriptableObject
    {
        // ===== 적 체력 =====
        [Header("적 체력 배율")]
        public float hpStart = 0.6f;
        public float hpEnd = 15f;
        [Tooltip("X: 게임 진행(0~1), Y: 보간(0~1). S자 권장.")]
        public AnimationCurve hpCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        // ===== 스폰 간격 =====
        [Header("스폰 간격 (초)")]
        public float intervalStart = 1.5f;
        public float intervalEnd = 0.3f;
        public AnimationCurve intervalCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        // ===== 최대 적 수 =====
        [Header("최대 동시 적 수 (2인 기준)")]
        public int maxEnemyStart = 20;
        public int maxEnemyEnd = 400;
        public AnimationCurve maxEnemyCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        // ===== 틱당 스폰 수 =====
        [Header("틱당 스폰 수")]
        [Range(1, 10)] public int spawnPerTickStart = 2;
        [Range(1, 10)] public int spawnPerTickEnd = 10;
        public AnimationCurve spawnPerTickCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        // ===== 경험치 시간 감쇠 =====
        [Header("경험치 시간 배율 (감소 곡선)")]
        [Tooltip("초반 EXP 배율 (빠른 레벨업)")]
        public float expTimeStart = 1.3f;
        [Tooltip("후반 EXP 배율 (레벨업 감속)")]
        public float expTimeEnd = 0.3f;
        public AnimationCurve expTimeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        // ===== 적 타입 비율 =====
        [Header("적 타입 비율 — 게임 시작 (t=0)")]
        [Range(0f, 1f)] public float chaserRatioStart = 1.0f;
        [Range(0f, 1f)] public float runnerRatioStart = 0f;
        [Range(0f, 1f)] public float tankRatioStart = 0f;
        [Range(0f, 1f)] public float swarmRatioStart = 0f;

        [Header("적 타입 비율 — 보스 직전 (t=1)")]
        [Range(0f, 1f)] public float chaserRatioEnd = 0.30f;
        [Range(0f, 1f)] public float runnerRatioEnd = 0.25f;
        [Range(0f, 1f)] public float tankRatioEnd = 0.15f;
        [Range(0f, 1f)] public float swarmRatioEnd = 0.30f;

        // ===== 인원수별 스케일링 =====
        [Header("인원수별 스케일링")]
        public PlayerScaling[] playerScalings = new PlayerScaling[]
        {
            new PlayerScaling { playerCount = 1, healthMultiplier = 0.6f, maxEnemyMultiplier = 0.6f, expMultiplier = 1.0f },
            new PlayerScaling { playerCount = 2, healthMultiplier = 1.0f, maxEnemyMultiplier = 1.0f, expMultiplier = 1.0f },
            new PlayerScaling { playerCount = 3, healthMultiplier = 1.4f, maxEnemyMultiplier = 1.3f, expMultiplier = 0.95f },
            new PlayerScaling { playerCount = 4, healthMultiplier = 1.8f, maxEnemyMultiplier = 1.6f, expMultiplier = 0.9f }
        };

        // ===== Swarm =====
        [Header("Swarm 설정")]
        public int swarmGroupMin = 5;
        public int swarmGroupMax = 10;

        // ===== 스폰 거리 =====
        [Header("스폰 거리 (카메라 시야 기준)")]
        public float spawnOffsetMin = 0.5f;
        public float spawnOffsetMax = 1.5f;
        public float playerSafeZone = 2.0f;
    }

    [Serializable]
    public struct PlayerScaling
    {
        public int playerCount;
        public float healthMultiplier;
        public float maxEnemyMultiplier;
        public float expMultiplier;
    }
}