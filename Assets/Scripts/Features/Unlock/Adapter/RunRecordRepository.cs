using System;
using UnityEngine;
using SwDreams.Shared.Platform.Adapter;

namespace SwDreams.Features.Unlock.Adapter
{
    /// <summary>
    /// 메타 진행도 영구 저장 IO (meta-unlock.md §8).
    /// 백엔드는 IPlatformService — Phase A=PlayerPrefs, Phase B/C=Stove/Steam 클라우드.
    ///
    /// JsonUtility 가 Dictionary / HashSet 미지원이라 RunStatsDto (List/array) 로 정규화.
    /// MetaProgressStore 가 자기 mutable state ↔ Dto 변환 담당.
    /// </summary>
    public static class RunRecordRepository
    {
        // platform-integration.md §10 컨벤션
        public const string KeyRunStats         = "meta.run_stats";
        public const string KeyUnlockedSkills   = "meta.unlocked_skills";
        public const string KeyUnlockedWeapons  = "meta.unlocked_weapons";
        public const string KeyUnlockedChars    = "meta.unlocked_characters";
        public const string KeyUnlockedBonuses  = "meta.unlocked_bonuses";

        public static RunStatsDto LoadStats()
        {
            var svc = PlatformBootstrap.Service;
            if (svc == null) return null;
            string json = svc.LoadData(KeyRunStats);
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                return JsonUtility.FromJson<RunStatsDto>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Unlock] {KeyRunStats} JSON parse 실패: {e.Message}");
                return null;
            }
        }

        public static void SaveStats(RunStatsDto dto)
        {
            var svc = PlatformBootstrap.Service;
            if (svc == null || dto == null) return;
            try
            {
                string json = JsonUtility.ToJson(dto);
                svc.SaveData(KeyRunStats, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Unlock] {KeyRunStats} JSON write 실패: {e.Message}");
            }
        }

        // ===== 언락 셋 IO (Unit 2 에서 사용) =====

        public static int[] LoadIdSet(string key)
        {
            var svc = PlatformBootstrap.Service;
            if (svc == null) return null;
            string json = svc.LoadData(key);
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var dto = JsonUtility.FromJson<IntArrayDto>(json);
                return dto?.values;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Unlock] {key} JSON parse 실패: {e.Message}");
                return null;
            }
        }

        public static void SaveIdSet(string key, int[] values)
        {
            var svc = PlatformBootstrap.Service;
            if (svc == null) return;
            try
            {
                var dto = new IntArrayDto { values = values ?? Array.Empty<int>() };
                svc.SaveData(key, JsonUtility.ToJson(dto));
            }
            catch (Exception e)
            {
                Debug.LogError($"[Unlock] {key} JSON write 실패: {e.Message}");
            }
        }
    }

    /// <summary>
    /// IRunStats 영구화 DTO. JsonUtility 직렬화 호환 (public field).
    /// </summary>
    [Serializable]
    public class RunStatsDto
    {
        public int totalKills;
        public int totalDeaths;
        public int totalRuns;
        public int totalClears;
        public int[] bossDefeatedIds;
        public int[] zonesVisitedIds;
        public int[] deathByEnemyIds;
    }

    /// <summary>JsonUtility 가 root int[] 직접 직렬화 못하므로 wrapper.</summary>
    [Serializable]
    public class IntArrayDto
    {
        public int[] values;
    }
}
