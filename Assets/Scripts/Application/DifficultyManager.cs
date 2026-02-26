using System;
using UnityEngine;
using SwDreams.Data;

namespace SwDreams.Application
{
    /// <summary>
    /// 난이도 곡선 서비스. 순수 C# 클래스 (MonoBehaviour 아님).
    /// DifficultyData SO를 참조하여 시간/인원수에 따른 수치를 반환.
    /// 
    /// DamageService, ExperienceService와 동일한 패턴.
    /// SpawnManager에서 생성하여 사용.
    /// 
    /// 사용 예:
    ///   var difficulty = new DifficultyManager(difficultyData);
    ///   float interval = difficulty.GetSpawnInterval(gameTime);
    ///   int maxEnemies = difficulty.GetMaxEnemyCount(gameTime, playerCount);
    /// </summary>
    public class DifficultyManager
    {
        private readonly DifficultyData data;

        public DifficultyManager(DifficultyData difficultyData)
        {
            data = difficultyData ?? throw new ArgumentNullException(nameof(difficultyData));
        }

        // ===== 시간 기반 쿼리 =====

        /// <summary>
        /// 현재 시간대의 스폰 간격(초).
        /// </summary>
        public float GetSpawnInterval(float gameTime)
        {
            var phase = GetCurrentPhase(gameTime);
            return phase.spawnInterval;
        }

        /// <summary>
        /// 한 틱에 스폰할 적 수 (Swarm 제외).
        /// </summary>
        public int GetSpawnPerTick(float gameTime)
        {
            var phase = GetCurrentPhase(gameTime);
            return Mathf.Max(1, phase.spawnPerTick);
        }

        /// <summary>
        /// 현재 시간대의 최대 동시 적 수. 인원수 스케일링 적용.
        /// </summary>
        public int GetMaxEnemyCount(float gameTime, int playerCount)
        {
            var phase = GetCurrentPhase(gameTime);
            var scaling = GetPlayerScaling(playerCount);
            return Mathf.RoundToInt(phase.maxEnemyCount * scaling.maxEnemyMultiplier);
        }

        /// <summary>
        /// 적 체력 배율. 시간 배율 × 인원수 배율.
        /// Enemy.Initialize의 hpMultiplier 파라미터에 전달.
        /// </summary>
        public float GetHealthMultiplier(float gameTime, int playerCount)
        {
            var phase = GetCurrentPhase(gameTime);
            var scaling = GetPlayerScaling(playerCount);
            return phase.healthMultiplier * scaling.healthMultiplier;
        }

        /// <summary>
        /// 경험치 배율. 인원수에 따라 조정.
        /// </summary>
        public float GetExpMultiplier(int playerCount)
        {
            var scaling = GetPlayerScaling(playerCount);
            return scaling.expMultiplier;
        }

        // ===== 적 타입 선택 =====

        /// <summary>
        /// 현재 시간대의 비율에 따라 스폰할 적 타입을 랜덤 반환.
        /// 가중치 랜덤 (Weighted Random).
        /// </summary>
        public EnemyType GetRandomEnemyType(float gameTime)
        {
            var phase = GetCurrentPhase(gameTime);

            float roll = UnityEngine.Random.value;
            float cumulative = 0f;

            cumulative += phase.chaserRatio;
            if (roll < cumulative) return EnemyType.Chaser;

            cumulative += phase.runnerRatio;
            if (roll < cumulative) return EnemyType.Runner;

            cumulative += phase.tankRatio;
            if (roll < cumulative) return EnemyType.Tank;

            return EnemyType.Swarm;
        }

        // ===== Swarm 설정 =====

        /// <summary>
        /// Swarm 한 그룹의 랜덤 마릿수.
        /// </summary>
        public int GetSwarmGroupSize()
        {
            return UnityEngine.Random.Range(data.swarmGroupMin, data.swarmGroupMax + 1);
        }

        // ===== 스폰 거리 =====

        public float SpawnMinDistance => data.spawnMinDistance;
        public float SpawnMaxDistance => data.spawnMaxDistance;
        public float PlayerSafeZone => data.playerSafeZone;

        // ===== 현재 Phase 정보 =====

        /// <summary>
        /// 현재 시간대 이름 (디버그용).
        /// </summary>
        public string GetCurrentPhaseName(float gameTime)
        {
            return GetCurrentPhase(gameTime).phaseName;
        }

        /// <summary>
        /// 보스 등장 시간에 도달했는지 (10분).
        /// </summary>
        public bool IsBossTime(float gameTime)
        {
            if (data.spawnPhases.Length == 0) return false;
            var lastPhase = data.spawnPhases[data.spawnPhases.Length - 1];
            return gameTime >= lastPhase.endTime;
        }

        // ===== 내부 =====

        private SpawnPhase GetCurrentPhase(float gameTime)
        {
            // 시간에 맞는 Phase 찾기. 못 찾으면 마지막 Phase 반환.
            for (int i = 0; i < data.spawnPhases.Length; i++)
            {
                var phase = data.spawnPhases[i];
                if (gameTime >= phase.startTime && gameTime < phase.endTime)
                    return phase;
            }

            // 마지막 Phase 이후 → 마지막 Phase 수치 유지
            if (data.spawnPhases.Length > 0)
                return data.spawnPhases[data.spawnPhases.Length - 1];

            // fallback (데이터 없음 — 있으면 안 되지만 방어)
            Debug.LogWarning("[DifficultyManager] SpawnPhase 데이터 없음!");
            return new SpawnPhase
            {
                spawnInterval = 2.5f,
                maxEnemyCount = 30,
                healthMultiplier = 1.0f,
                spawnPerTick = 1,
                chaserRatio = 1f
            };
        }

        private PlayerScaling GetPlayerScaling(int playerCount)
        {
            // 정확히 일치하는 인원수 찾기
            for (int i = 0; i < data.playerScalings.Length; i++)
            {
                if (data.playerScalings[i].playerCount == playerCount)
                    return data.playerScalings[i];
            }

            // 못 찾으면 가장 가까운 것 반환
            if (data.playerScalings.Length > 0)
            {
                int closestIdx = 0;
                int closestDiff = int.MaxValue;
                for (int i = 0; i < data.playerScalings.Length; i++)
                {
                    int diff = Mathf.Abs(data.playerScalings[i].playerCount - playerCount);
                    if (diff < closestDiff)
                    {
                        closestDiff = diff;
                        closestIdx = i;
                    }
                }
                return data.playerScalings[closestIdx];
            }

            // fallback
            return new PlayerScaling
            {
                playerCount = playerCount,
                healthMultiplier = 1f,
                maxEnemyMultiplier = 1f,
                expMultiplier = 1f
            };
        }
    }
}