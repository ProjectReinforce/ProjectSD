using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using SwDreams.Shared.Managers;

namespace SwDreams.Features.Unlock.Adapter
{
    /// <summary>
    /// 멀티플레이 권위 모델 (D5) — 자기 진행도가 자기 게임에 반영.
    ///
    /// 동작:
    /// - 자기 클라가 자기 unlocked 셋을 PhotonPlayer.CustomProperties 로 공유 (mu_skills/mu_weapons/mu_chars).
    /// - 호스트가 풀 결정 시 대상 플레이어 ActorNumber 로 그 플레이어의 셋 조회.
    /// - 자기 ownerActor 인 경우 로컬 UnlockTracker 가 SSOT (CustomProperties 도착 race 회피).
    ///
    /// 셋업: GameManager.Awake 에서 GetOrCreate. OnJoinedRoom 시 자기 셋 push.
    /// 셋 변경 시점 = UnlockTracker.OnNewUnlocks 발화 직후 (다음 게임부터 적용 — 본 런은 영향 X, D11).
    /// </summary>
    public class UnlockSetSync : MonoBehaviourPunCallbacks
    {
        public static UnlockSetSync Instance { get; private set; }

        // CustomProperties key (Photon 키 충돌 회피용 prefix mu_).
        private const string KeySkills       = "mu_skills";
        private const string KeyWeapons      = "mu_weapons";
        private const string KeyCharacters   = "mu_chars";
        private const string KeyRefreshBonus = "mu_refresh_bonus";  // int — RefreshCharge 마일스톤 합계

        public static UnlockSetSync GetOrCreate()
        {
            if (Instance != null) return Instance;
            var go = new GameObject(nameof(UnlockSetSync));
            return go.AddComponent<UnlockSetSync>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // UnlockTracker 의 OnNewUnlocks 구독 — 새 언락 시 자기 셋 다시 push.
            // 이렇게 하면 다음 게임 시작 시 다른 플레이어가 자기 셋을 즉시 본다.
            // (실제 효과는 D11 일괄 평가 정책으로 다음 런부터지만, 셋 push 자체는 즉시.)
            var tracker = UnlockTracker.GetOrCreate();
            tracker.OnNewUnlocks += OnNewUnlocksHandler;
        }

        // 메서드 핸들러로 분리 — OnDestroy 에서 detach 가능 (lambda 면 detach 못 함).
        private void OnNewUnlocksHandler(System.Collections.Generic.List<Domain.UnlockableId> _)
        {
            PushSelf();
        }

        public override void OnEnable()
        {
            base.OnEnable();
            // 이미 룸에 있으면 즉시 push (씬 재진입 시점 race 회피).
            if (PhotonNetwork.InRoom) PushSelf();
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            if (UnlockTracker.Instance != null)
                UnlockTracker.Instance.OnNewUnlocks -= OnNewUnlocksHandler;
            Instance = null;
        }

        public override void OnJoinedRoom()
        {
            base.OnJoinedRoom();
            PushSelf();
        }

        /// <summary>자기 unlocked 셋을 PhotonPlayer.CustomProperties 에 push.</summary>
        public static void PushSelf()
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null) return;
            var tracker = UnlockTracker.Instance;
            if (tracker == null) return;

            var props = new Hashtable();
            props[KeySkills]       = ToIntArray(tracker.UnlockedSkillIds);
            props[KeyCharacters]   = ToIntArray(tracker.UnlockedCharacterIds);
            props[KeyWeapons]      = WeaponIdsToIndices(tracker.UnlockedWeaponIds);
            props[KeyRefreshBonus] = tracker.BonusRefreshCharges;

            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
            Debug.Log($"[UnlockSetSync] Pushed self: skills={tracker.UnlockedSkillIds.Count}, " +
                      $"weapons={tracker.UnlockedWeaponIds.Count}, chars={tracker.UnlockedCharacterIds.Count}, " +
                      $"refreshBonus=+{tracker.BonusRefreshCharges}");
        }

        // ===== 외부 query API (SkillManager/PlayerWeaponInventory/CharacterSelectUI 가 호출) =====

        /// <summary>지정 플레이어가 해당 스킬을 언락했는지. unlockConditions 비어있으면 호출자가 통과시킴.</summary>
        public static bool IsSkillUnlocked(int actorNumber, int skillId)
        {
            // 자기 자신 → 로컬 UnlockTracker SSOT
            if (IsSelf(actorNumber))
            {
                bool selfResult = UnlockTracker.Instance != null && UnlockTracker.Instance.IsSkillUnlocked(skillId);
                if (verboseLogging)
                    Debug.Log($"[UnlockSetSync] IsSkillUnlocked(self actor={actorNumber}, skill={skillId}) = {selfResult}");
                return selfResult;
            }

            // 다른 플레이어 → CustomProperties
            bool propResult = ContainsInPropArray(actorNumber, KeySkills, skillId);
            if (verboseLogging)
                DebugDumpForeignActorProps(actorNumber, KeySkills, skillId, propResult);
            return propResult;
        }

        /// <summary>디버그 — true 면 IsSkillUnlocked / IsWeaponUnlocked 시 상세 로그.</summary>
        public static bool verboseLogging = false;

        private static void DebugDumpForeignActorProps(int actorNumber, string key, int target, bool result)
        {
            Player player = null;
            if (PhotonNetwork.CurrentRoom != null)
                PhotonNetwork.CurrentRoom.Players.TryGetValue(actorNumber, out player);
            if (player == null)
            {
                Debug.LogWarning($"[UnlockSetSync] actor={actorNumber} 의 Photon Player 인스턴스 없음");
                return;
            }
            string arrStr = "(no key)";
            if (player.CustomProperties != null && player.CustomProperties.TryGetValue(key, out var raw))
            {
                if (raw is int[] arr) arrStr = "[" + string.Join(",", arr) + "]";
                else arrStr = raw?.ToString() ?? "(null raw)";
            }
            Debug.Log($"[UnlockSetSync] IsSkillUnlocked(actor={actorNumber}, target={target}) = {result}, " +
                      $"props[{key}]={arrStr}");
        }

        /// <summary>지정 플레이어가 해당 무기(합성 결과물)를 언락했는지.</summary>
        public static bool IsWeaponUnlocked(int actorNumber, string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId)) return true;

            if (IsSelf(actorNumber))
                return UnlockTracker.Instance != null && UnlockTracker.Instance.IsWeaponUnlocked(weaponId);

            // 다른 플레이어: CustomProperties 에 저장된 인덱스 → DB lookup
            int weaponIndex = ResolveWeaponIndex(weaponId);
            if (weaponIndex < 0) return false;
            return ContainsInPropArray(actorNumber, KeyWeapons, weaponIndex);
        }

        /// <summary>
        /// 지정 플레이어가 해당 캐릭터를 언락했는지.
        /// CharacterSelectUI 는 자기 PC 만 보면 되므로 보통 자기 self 호출.
        /// </summary>
        public static bool IsCharacterUnlocked(int actorNumber, int characterId)
        {
            if (IsSelf(actorNumber))
                return UnlockTracker.Instance != null && UnlockTracker.Instance.IsCharacterUnlocked(characterId);

            return ContainsInPropArray(actorNumber, KeyCharacters, characterId);
        }

        /// <summary>
        /// 지정 플레이어의 RefreshCharge 마일스톤 보너스 합계 (D7).
        /// 호스트가 LevelUpManager 의 새로고침 lazy init 시 본 값을 가산하여 D5 보장.
        /// </summary>
        public static int GetRefreshBonusFor(int actorNumber)
        {
            if (IsSelf(actorNumber))
                return UnlockTracker.Instance != null ? UnlockTracker.Instance.BonusRefreshCharges : 0;

            Player player = null;
            if (PhotonNetwork.CurrentRoom != null)
                PhotonNetwork.CurrentRoom.Players.TryGetValue(actorNumber, out player);
            if (player == null || player.CustomProperties == null) return 0;
            if (!player.CustomProperties.TryGetValue(KeyRefreshBonus, out var raw)) return 0;
            return raw is int v ? v : 0;
        }

        // ===== 내부 헬퍼 =====

        private static bool IsSelf(int actorNumber) =>
            PhotonNetwork.LocalPlayer != null && PhotonNetwork.LocalPlayer.ActorNumber == actorNumber;

        private static bool ContainsInPropArray(int actorNumber, string key, int target)
        {
            Player player = null;
            if (PhotonNetwork.CurrentRoom != null)
                PhotonNetwork.CurrentRoom.Players.TryGetValue(actorNumber, out player);
            if (player == null || player.CustomProperties == null) return false;
            if (!player.CustomProperties.TryGetValue(key, out var raw)) return false;
            if (raw is int[] arr)
            {
                for (int i = 0; i < arr.Length; i++)
                    if (arr[i] == target) return true;
            }
            return false;
        }

        private static int[] ToIntArray(IReadOnlyCollection<int> set)
        {
            if (set == null) return new int[0];
            var arr = new int[set.Count];
            int i = 0;
            foreach (var v in set) arr[i++] = v;
            return arr;
        }

        private static int[] WeaponIdsToIndices(IReadOnlyCollection<string> ids)
        {
            var db = GameManager.Instance?.WeaponDB;
            if (db == null || db.All == null || ids == null) return new int[0];
            var list = new List<int>();
            for (int i = 0; i < db.All.Count; i++)
            {
                var w = db.All[i];
                if (w != null && !string.IsNullOrEmpty(w.weaponId) && ids.Contains(w.weaponId))
                    list.Add(i);
            }
            return list.ToArray();
        }

        private static int ResolveWeaponIndex(string weaponId)
        {
            var db = GameManager.Instance?.WeaponDB;
            if (db == null || db.All == null) return -1;
            for (int i = 0; i < db.All.Count; i++)
            {
                var w = db.All[i];
                if (w != null && w.weaponId == weaponId) return i;
            }
            return -1;
        }
    }
}
