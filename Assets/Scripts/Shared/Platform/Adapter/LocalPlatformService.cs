using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using SwDreams.Shared.Domain;
using SwDreams.Shared.Platform.Domain;

namespace SwDreams.Shared.Platform.Adapter
{
    /// <summary>
    /// 로컬(개발/Editor) 플랫폼 stub.
    ///
    /// Phase A 도입 시 PlayerPrefs 백엔드로 강화 (plan §1, D9).
    ///   - SaveData/LoadData → PlayerPrefs 영구 저장 (메타 언락 필수)
    ///   - IncrementStat → PlayerPrefs int 누적
    ///   - UnlockAchievement → PlayerPrefs bool flag
    /// 메모리 캐시는 PlayerPrefs read 의 hot 경로 최적화용.
    ///
    /// Stove/Steam SDK 도입 시 동일 인터페이스로 갈아끼우면 클라우드 세이브 자동 적용.
    /// </summary>
    public class LocalPlatformService : IPlatformService
    {
        public bool IsInitialized { get; private set; }

        // PlayerPrefs key prefix (충돌 방지)
        private const string PrefAchievement = "platform.ach.";   // bool 0/1
        private const string PrefStat        = "platform.stat.";  // int
        private const string PrefData        = "platform.data."; // string (json)

        /// <summary>
        /// Editor bridge — ParrelSync clone 별 PlayerPrefs 격리.
        /// ParrelSync 인스턴스가 같은 ProjectSettings 를 공유해 같은 PlayerPrefs namespace 를
        /// 쓰므로 멀티 테스트 시 D5 검증이 깨진다. Editor 코드 `ParrelSyncBridge` 가
        /// `InitializeOnLoad` 시점에 이 hook 을 셋업해 clone 마다 고유 prefix 부여.
        /// 빌드 환경 / ParrelSync 미설치 환경에선 null → 빈 prefix (기존 동작).
        /// </summary>
        public static System.Func<string> CloneSuffixProvider;

        private static string GetCloneSuffix()
            => CloneSuffixProvider != null ? (CloneSuffixProvider() ?? "") : "";

        // ===== 공용 key 빌더 (디버그 윈도우 Reset All 도 사용) =====

        public static string MakeAchievementKey(string id) => GetCloneSuffix() + PrefAchievement + id;
        public static string MakeStatKey(string id)        => GetCloneSuffix() + PrefStat + id;
        public static string MakeDataKey(string id)        => GetCloneSuffix() + PrefData + id;

        // 메모리 캐시 (PlayerPrefs read hot 경로)
        private readonly HashSet<string> unlockedAchievements = new HashSet<string>();
        private readonly Dictionary<string, int> stats = new Dictionary<string, int>();
        private readonly Dictionary<string, string> savedData = new Dictionary<string, string>();

        public void Initialize()
        {
            // PlayerPrefs 는 Unity 가 자동 로드. 본 클래스는 lazy 캐시만 사용.
            IsInitialized = true;
            Debug.Log("[Platform/Local] Initialized (PlayerPrefs backend)");
        }

        public void Shutdown()
        {
            // 미저장 데이터 보장 — PlayerPrefs.Save 는 Unity 종료 시 자동이지만 명시적 호출.
            PlayerPrefs.Save();
            IsInitialized = false;
            Debug.Log("[Platform/Local] Shutdown");
        }

        public PlatformUserProfile GetLocalUser()
        {
            string nick = "LocalPlayer";
            int actor = 0;
            if (PhotonNetwork.IsConnected && PhotonNetwork.LocalPlayer != null)
            {
                if (!string.IsNullOrEmpty(PhotonNetwork.LocalPlayer.NickName))
                    nick = PhotonNetwork.LocalPlayer.NickName;
                actor = PhotonNetwork.LocalPlayer.ActorNumber;
            }
            return new PlatformUserProfile
            {
                UserId = $"local-{actor}",
                DisplayName = nick,
                Source = PlatformType.Local,
            };
        }

        public void UnlockAchievement(string achievementId)
        {
            if (string.IsNullOrEmpty(achievementId)) return;
            if (!unlockedAchievements.Add(achievementId)) return;  // 이미 unlock

            string key = MakeAchievementKey(achievementId);
            if (PlayerPrefs.GetInt(key, 0) == 1) return;  // PlayerPrefs 에 이미 있음
            PlayerPrefs.SetInt(key, 1);
            PlayerPrefs.Save();
            Debug.Log($"[Platform/Local] Achievement unlocked: {achievementId}");
        }

        public bool IsAchievementUnlocked(string achievementId)
        {
            if (string.IsNullOrEmpty(achievementId)) return false;
            if (unlockedAchievements.Contains(achievementId)) return true;
            bool stored = PlayerPrefs.GetInt(MakeAchievementKey(achievementId), 0) == 1;
            if (stored) unlockedAchievements.Add(achievementId);
            return stored;
        }

        public void IncrementStat(string statId, int delta)
        {
            if (string.IsNullOrEmpty(statId) || delta == 0) return;

            string key = MakeStatKey(statId);
            int current;
            if (!stats.TryGetValue(statId, out current))
                current = PlayerPrefs.GetInt(key, 0);
            int next = current + delta;
            stats[statId] = next;
            PlayerPrefs.SetInt(key, next);
            // PlayerPrefs.Save 는 매 호출마다 하지 않음 (성능). Shutdown / SaveData 에서 일괄 flush.
        }

        public void SubmitRunResult(GameResult result)
        {
            if (result == null) return;
            Debug.Log($"[Platform/Local] SubmitRunResult: cleared={result.IsCleared}, " +
                      $"time={result.PlayTime:F1}s, kills={result.TotalKills}");
            // Phase B/C 에서 SDK 리더보드/통계 API 호출.
        }

        public void SaveData(string key, string json)
        {
            if (string.IsNullOrEmpty(key)) return;
            string prefKey = MakeDataKey(key);
            savedData[key] = json ?? string.Empty;
            PlayerPrefs.SetString(prefKey, json ?? string.Empty);
            PlayerPrefs.Save();
            Debug.Log($"[Platform/Local] SaveData[{key}] = {(json != null ? json.Length : 0)} chars (key={prefKey})");
        }

        public string LoadData(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (savedData.TryGetValue(key, out var cached)) return cached;
            string prefKey = MakeDataKey(key);
            if (!PlayerPrefs.HasKey(prefKey)) return null;
            string v = PlayerPrefs.GetString(prefKey, null);
            savedData[key] = v;
            return v;
        }
    }
}
