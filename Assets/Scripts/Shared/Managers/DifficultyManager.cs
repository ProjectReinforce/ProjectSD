using System;
using SwDreams.Features.Enemy.Adapter.Data;
using UnityEngine;
using SwDreams.Shared.Data;

namespace SwDreams.Shared.Managers
{
    /// <summary>
    /// 난이도 곡선 서비스. AnimationCurve 기반.
    /// DifficultyData SO의 커브를 Evaluate하여 값 반환.
    ///
    /// 공식: value = start + (end - start) * curve.Evaluate(t)
    /// t = gameTime / gameEndTime (0~1)
    /// </summary>
    public class DifficultyManager
    {
        private readonly DifficultyData data;
        private readonly float gameEndTime;

        public DifficultyManager(DifficultyData difficultyData, float gameEndTime)
        {
            data = difficultyData ?? throw new ArgumentNullException(nameof(difficultyData));
            this.gameEndTime = Mathf.Max(1f, gameEndTime);
        }

        private float GetT(float gameTime)
        {
            return Mathf.Clamp01(gameTime / gameEndTime);
        }

        private float Eval(float start, float end, AnimationCurve curve, float t)
        {
            return start + (end - start) * curve.Evaluate(t);
        }

        // ===== 시간 기반 쿼리 =====

        public float GetSpawnInterval(float gameTime)
        {
            float t = GetT(gameTime);
            return Mathf.Max(0.1f, Eval(data.intervalStart, data.intervalEnd, data.intervalCurve, t));
        }

        public int GetSpawnPerTick(float gameTime)
        {
            float t = GetT(gameTime);
            float value = Eval(data.spawnPerTickStart, data.spawnPerTickEnd, data.spawnPerTickCurve, t);
            return Mathf.Max(1, Mathf.RoundToInt(value));
        }

        public int GetMaxEnemyCount(float gameTime, int playerCount)
        {
            float t = GetT(gameTime);
            float base_ = Eval(data.maxEnemyStart, data.maxEnemyEnd, data.maxEnemyCurve, t);
            var scaling = GetPlayerScaling(playerCount);
            return Mathf.Max(1, Mathf.RoundToInt(base_ * scaling.maxEnemyMultiplier));
        }

        public float GetHealthMultiplier(float gameTime, int playerCount)
        {
            float t = GetT(gameTime);
            float base_ = Eval(data.hpStart, data.hpEnd, data.hpCurve, t);
            var scaling = GetPlayerScaling(playerCount);
            return base_ * scaling.healthMultiplier;
        }

        public float GetExpMultiplier(float gameTime, int playerCount)
        {
            float t = GetT(gameTime);
            float timeMul = Eval(data.expTimeStart, data.expTimeEnd, data.expTimeCurve, t);
            var scaling = GetPlayerScaling(playerCount);
            return timeMul * scaling.expMultiplier;
        }

        // 하위 호환
        public float GetExpMultiplier(int playerCount)
        {
            var scaling = GetPlayerScaling(playerCount);
            return scaling.expMultiplier;
        }

        // ===== 적 타입 선택 =====

        public EnemyType GetRandomEnemyType(float gameTime)
        {
            float t = GetT(gameTime);

            // 각 타입의 가중치(합계가 1.0 일 필요 없음 — 아래에서 정규화).
            float chaser = Mathf.Max(0f, Mathf.Lerp(data.chaserRatioStart, data.chaserRatioEnd, t));
            float runner = Mathf.Max(0f, Mathf.Lerp(data.runnerRatioStart, data.runnerRatioEnd, t));
            float tank = Mathf.Max(0f, Mathf.Lerp(data.tankRatioStart, data.tankRatioEnd, t));
            float swarm = Mathf.Max(0f, Mathf.Lerp(data.swarmRatioStart, data.swarmRatioEnd, t));
            float ranged = Mathf.Max(0f, Mathf.Lerp(data.rangedRatioStart, data.rangedRatioEnd, t));

            float total = chaser + runner + tank + swarm + ranged;
            if (total <= 0.0001f) return EnemyType.Chaser; // 안전 폴백

            // 합계로 나누어 상대 확률로 선택 (어떤 항목만 1로 두면 그것만 100%).
            float roll = UnityEngine.Random.value * total;
            float cumulative = 0f;

            cumulative += chaser;
            if (roll < cumulative) return EnemyType.Chaser;

            cumulative += runner;
            if (roll < cumulative) return EnemyType.Runner;

            cumulative += tank;
            if (roll < cumulative) return EnemyType.Tank;

            cumulative += ranged;
            if (roll < cumulative) return EnemyType.Ranged;

            return EnemyType.Swarm;
        }

        // ===== Swarm =====

        public int GetSwarmGroupSize()
        {
            return UnityEngine.Random.Range(data.swarmGroupMin, data.swarmGroupMax + 1);
        }

        // ===== 스폰 거리 =====

        public float SpawnOffsetMin => data.spawnOffsetMin;
        public float SpawnOffsetMax => data.spawnOffsetMax;
        public float PlayerSafeZone => data.playerSafeZone;

        // ===== 보스 타이밍 =====

        public bool IsBossTime(float gameTime)
        {
            return gameTime >= gameEndTime;
        }

        // ===== 디버그 =====

        public string GetCurrentPhaseName(float gameTime)
        {
            float t = GetT(gameTime);
            if (t < 0.05f) return "워밍업";
            if (t < 0.17f) return "초반";
            if (t < 0.37f) return "중반 1";
            if (t < 0.60f) return "중반 2";
            if (t < 0.87f) return "후반";
            return "전초전";
        }

        // ===== 내부 =====

        private PlayerScaling GetPlayerScaling(int playerCount)
        {
            for (int i = 0; i < data.playerScalings.Length; i++)
            {
                if (data.playerScalings[i].playerCount == playerCount)
                    return data.playerScalings[i];
            }

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