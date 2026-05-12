using System;
using System.Collections.Generic;
using UnityEngine;
using SwDreams.Features.Unlock.Domain;
using SwDreams.Features.Unlock.Adapter.Data;
using SwDreams.Features.Skill.Adapter.Data;
using SwDreams.Features.Weapon.Adapter.Data;
using SwDreams.Features.Character.Adapter.Data;
using SwDreams.Features.Progression.Adapter;
using SwDreams.Shared.Domain.Events;
using SwDreams.Shared.Managers;
using SwDreams.Shared.Platform.Adapter;

namespace SwDreams.Features.Unlock.Adapter
{
    /// <summary>
    /// 메타 언락 평가 / 영구 저장 / 신규 언락 발화 (meta-unlock.md §10, D11).
    ///
    /// - 단일 인스턴스 (DontDestroyOnLoad). lazy GetOrCreate 패턴.
    /// - RunEventBus.RunEnded 구독 → MetaProgressStore 의 IRunStats 보고 일괄 평가.
    /// - 새로 충족된 보상을 meta.unlocked_* 키에 추가 저장 + OnNewUnlocks 발화.
    /// - 평가 시점 = 런 종료 후 1회 (D11) — 토스트 UX 일관 + 매 이벤트마다 카탈로그 순회 회피.
    ///
    /// 데이터 의존성:
    /// - SkillDatabase : LevelUpManager.Instance.SkillDB (이미 셋업됨)
    /// - WeaponDatabase: GameManager.Instance.WeaponDB
    /// - CharacterDatabase: GameManager.Instance.CharacterDB
    /// - UnlockCatalog: GameManager.Instance.UnlockCatalog (사용자 신규 셋업, 없으면 RefreshCharge 0)
    /// - MetaProgressStore: 자기 PC IRunStats
    /// </summary>
    public class UnlockTracker : MonoBehaviour
    {
        public static UnlockTracker Instance { get; private set; }

        // 영구 언락 셋 (메모리 캐시 + PlayerPrefs SSOT)
        private readonly HashSet<int> unlockedSkillIds = new HashSet<int>();
        private readonly HashSet<string> unlockedWeaponIds = new HashSet<string>();  // weaponId 가 string
        private readonly HashSet<int> unlockedCharacterIds = new HashSet<int>();
        private readonly HashSet<int> unlockedBonusIndices = new HashSet<int>();     // RefreshChargeNode index

        /// <summary>
        /// 한 런 종료 시 새로 언락된 항목 리스트 발화.
        /// ResultManager (Unit 3) 가 구독해 결과 화면 토스트 표시.
        /// </summary>
        public event Action<List<UnlockableId>> OnNewUnlocks;

        // ===== 외부 read-only 접근 =====
        public IReadOnlyCollection<int> UnlockedSkillIds => unlockedSkillIds;
        public IReadOnlyCollection<string> UnlockedWeaponIds => unlockedWeaponIds;
        public IReadOnlyCollection<int> UnlockedCharacterIds => unlockedCharacterIds;

        /// <summary>
        /// LevelUpManager 초기 충전에 가산되는 영구 진행도 보너스 (D7 — RefreshCharge 마일스톤 합산).
        /// </summary>
        public int BonusRefreshCharges
        {
            get
            {
                var catalog = GameManager.Instance?.UnlockCatalog;
                if (catalog == null || catalog.refreshChargeNodes == null) return 0;
                int total = 0;
                foreach (var idx in unlockedBonusIndices)
                {
                    if (idx < 0 || idx >= catalog.refreshChargeNodes.Count) continue;
                    total += catalog.refreshChargeNodes[idx].amount;
                }
                return total;
            }
        }

        public static UnlockTracker GetOrCreate()
        {
            if (Instance != null) return Instance;
            var go = new GameObject(nameof(UnlockTracker));
            return go.AddComponent<UnlockTracker>();
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

            LoadUnlockSets();

            RunEventBus.Instance.RunEnded += OnRunEnded;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            RunEventBus.Instance.RunEnded -= OnRunEnded;
            Instance = null;
        }

        private void LoadUnlockSets()
        {
            // PlatformBootstrap 이 자기보다 늦게 생성되는 chain 시점(NetworkManager.Awake 의 UnlockSetSync 경유)
            // 에서도 PlayerPrefs 로드가 동작하도록 보장.
            // 미보장 시: PlatformBootstrap.Service==null → RunRecordRepository.LoadIdSet 가 null 반환 →
            // 메모리 캐시 빈 셋 → Play 재시작마다 PlayerPrefs 의 영구 데이터 무시되는 race.
            if (PlatformBootstrap.Instance == null)
                PlatformBootstrap.GetOrCreate();

            int[] skills = RunRecordRepository.LoadIdSet(RunRecordRepository.KeyUnlockedSkills);
            if (skills != null) foreach (var id in skills) unlockedSkillIds.Add(id);

            int[] characters = RunRecordRepository.LoadIdSet(RunRecordRepository.KeyUnlockedChars);
            if (characters != null) foreach (var id in characters) unlockedCharacterIds.Add(id);

            int[] bonuses = RunRecordRepository.LoadIdSet(RunRecordRepository.KeyUnlockedBonuses);
            if (bonuses != null) foreach (var id in bonuses) unlockedBonusIndices.Add(id);

            // weaponId 는 string — 별도 로직 필요. 일단 RunRecordRepository.LoadIdSet 은 int[] 기반.
            // weapon 은 WeaponData 인덱스(Database 내) 로 저장.
            int[] weaponIndices = RunRecordRepository.LoadIdSet(RunRecordRepository.KeyUnlockedWeapons);
            if (weaponIndices != null)
            {
                var db = GameManager.Instance?.WeaponDB;
                if (db != null && db.All != null)
                {
                    foreach (var idx in weaponIndices)
                    {
                        if (idx < 0 || idx >= db.All.Count) continue;
                        var w = db.All[idx];
                        if (w != null && !string.IsNullOrEmpty(w.weaponId))
                            unlockedWeaponIds.Add(w.weaponId);
                    }
                }
            }

            Debug.Log($"[UnlockTracker] Loaded: skills={unlockedSkillIds.Count}, " +
                      $"weapons={unlockedWeaponIds.Count}, chars={unlockedCharacterIds.Count}, " +
                      $"bonuses={unlockedBonusIndices.Count}, refreshBonus=+{BonusRefreshCharges}");
        }

        private void OnRunEnded(bool isCleared)
        {
            // MetaProgressStore.OnRunEnded 가 먼저 등록돼 totalRuns/totalClears 갱신 후 우리 차례.
            // 등록 순서 (RunEventBus 의 .NET Action 멀티캐스트) — GameManager.Awake 에서
            // MetaProgressStore.GetOrCreate 가 먼저, 그 다음 UnlockTracker.GetOrCreate.
            var stats = MetaProgressStore.Instance as IRunStats;
            if (stats == null)
            {
                Debug.LogWarning("[UnlockTracker] MetaProgressStore.Instance 없음 — 평가 스킵");
                return;
            }

            var newUnlocks = new List<UnlockableId>();

            EvaluateSkills(stats, newUnlocks);
            EvaluateWeapons(stats, newUnlocks);
            EvaluateCharacters(stats, newUnlocks);
            EvaluateRefreshBonuses(stats, newUnlocks);

            if (newUnlocks.Count > 0)
            {
                SaveUnlockSets();
                Debug.Log($"[UnlockTracker] 신규 언락 {newUnlocks.Count} 건: " +
                          $"{string.Join(", ", newUnlocks)}");
                OnNewUnlocks?.Invoke(newUnlocks);
            }
        }

        private void EvaluateSkills(IRunStats stats, List<UnlockableId> output)
        {
            var db = LevelUpManager.Instance?.SkillDB;
            if (db == null) return;

            EvaluateSkillArray(db.activeSkills, stats, output);
            EvaluateSkillArray(db.passiveSkills, stats, output);
            EvaluateSkillArray(db.chaosSkills, stats, output);
            EvaluateSkillArray(db.evolvedSkills, stats, output);
        }

        private void EvaluateSkillArray(SkillData[] arr, IRunStats stats, List<UnlockableId> output)
        {
            if (arr == null) return;
            for (int i = 0; i < arr.Length; i++)
            {
                var s = arr[i];
                if (s == null) continue;
                if (s.unlockConditions == null || s.unlockConditions.Count == 0) continue; // 처음부터 해금
                if (unlockedSkillIds.Contains(s.skillId)) continue;                          // 이미 해금
                if (!UnlockEvaluator.EvaluateAll(s.unlockConditions, stats)) continue;       // 조건 미충족

                unlockedSkillIds.Add(s.skillId);
                output.Add(new UnlockableId(UnlockableType.Skill, s.skillId));
            }
        }

        private void EvaluateWeapons(IRunStats stats, List<UnlockableId> output)
        {
            var db = GameManager.Instance?.WeaponDB;
            if (db == null || db.All == null) return;

            for (int i = 0; i < db.All.Count; i++)
            {
                var w = db.All[i];
                if (w == null || string.IsNullOrEmpty(w.weaponId)) continue;
                if (w.unlockConditions == null || w.unlockConditions.Count == 0) continue;
                if (unlockedWeaponIds.Contains(w.weaponId)) continue;
                if (!UnlockEvaluator.EvaluateAll(w.unlockConditions, stats)) continue;

                unlockedWeaponIds.Add(w.weaponId);
                // weaponId 는 string 이라 UnlockableId.id 에 인덱스 사용 (db.All 내 index).
                output.Add(new UnlockableId(UnlockableType.Weapon, i));
            }
        }

        private void EvaluateCharacters(IRunStats stats, List<UnlockableId> output)
        {
            var db = GameManager.Instance?.CharacterDB;
            if (db == null || db.characters == null) return;

            for (int i = 0; i < db.characters.Length; i++)
            {
                var c = db.characters[i];
                if (c == null) continue;
                if (c.unlockConditions == null || c.unlockConditions.Count == 0) continue;
                if (unlockedCharacterIds.Contains(c.id)) continue;
                if (!UnlockEvaluator.EvaluateAll(c.unlockConditions, stats)) continue;

                unlockedCharacterIds.Add(c.id);
                output.Add(new UnlockableId(UnlockableType.Character, c.id));
            }
        }

        private void EvaluateRefreshBonuses(IRunStats stats, List<UnlockableId> output)
        {
            var catalog = GameManager.Instance?.UnlockCatalog;
            if (catalog == null || catalog.refreshChargeNodes == null) return;

            for (int i = 0; i < catalog.refreshChargeNodes.Count; i++)
            {
                if (unlockedBonusIndices.Contains(i)) continue;
                var node = catalog.refreshChargeNodes[i];
                if (!UnlockEvaluator.Evaluate(node.condition, stats)) continue;

                unlockedBonusIndices.Add(i);
                output.Add(new UnlockableId(UnlockableType.RefreshCharge, i));
            }
        }

        private void SaveUnlockSets()
        {
            // skill ids
            var skillArr = new int[unlockedSkillIds.Count];
            int k = 0;
            foreach (var id in unlockedSkillIds) skillArr[k++] = id;
            RunRecordRepository.SaveIdSet(RunRecordRepository.KeyUnlockedSkills, skillArr);

            // character ids
            var charArr = new int[unlockedCharacterIds.Count];
            k = 0;
            foreach (var id in unlockedCharacterIds) charArr[k++] = id;
            RunRecordRepository.SaveIdSet(RunRecordRepository.KeyUnlockedChars, charArr);

            // bonus indices
            var bonusArr = new int[unlockedBonusIndices.Count];
            k = 0;
            foreach (var id in unlockedBonusIndices) bonusArr[k++] = id;
            RunRecordRepository.SaveIdSet(RunRecordRepository.KeyUnlockedBonuses, bonusArr);

            // weapon: string → DB 내 index 로 변환 후 저장
            var db = GameManager.Instance?.WeaponDB;
            if (db != null && db.All != null)
            {
                var indices = new List<int>();
                for (int i = 0; i < db.All.Count; i++)
                {
                    var w = db.All[i];
                    if (w != null && unlockedWeaponIds.Contains(w.weaponId))
                        indices.Add(i);
                }
                RunRecordRepository.SaveIdSet(RunRecordRepository.KeyUnlockedWeapons, indices.ToArray());
            }
        }

        // ===== UnlockSetSync 가 사용할 query API =====

        public bool IsSkillUnlocked(int skillId) =>
            // 처음부터 해금된 스킬은 평가 시 unlockConditions 비어있음 → 본 메서드는
            // "조건 충족으로 명시적 unlock" 만 추적. 풀 필터링 시 SO.unlockConditions.Count==0 도
            // 처음부터 해금으로 함께 통과시켜야 함 (UnlockSetSync 가 처리).
            unlockedSkillIds.Contains(skillId);

        public bool IsWeaponUnlocked(string weaponId) =>
            !string.IsNullOrEmpty(weaponId) && unlockedWeaponIds.Contains(weaponId);

        public bool IsCharacterUnlocked(int characterId) =>
            unlockedCharacterIds.Contains(characterId);

        // ===== 디버그 =====

        [ContextMenu("Debug: Print Unlocks")]
        public void DebugPrint()
        {
            Debug.Log($"[UnlockTracker] skills=[{string.Join(",", unlockedSkillIds)}], " +
                      $"weapons=[{string.Join(",", unlockedWeaponIds)}], " +
                      $"chars=[{string.Join(",", unlockedCharacterIds)}], " +
                      $"bonusIdx=[{string.Join(",", unlockedBonusIndices)}], " +
                      $"refreshBonus=+{BonusRefreshCharges}");
        }

        [ContextMenu("Debug: Reset Unlocks")]
        public void DebugReset()
        {
            unlockedSkillIds.Clear();
            unlockedWeaponIds.Clear();
            unlockedCharacterIds.Clear();
            unlockedBonusIndices.Clear();
            SaveUnlockSets();
            // CustomProperties 도 즉시 빈 셋으로 동기 — 멀티 측 일관성.
            UnlockSetSync.PushSelf();
            Debug.Log("[UnlockTracker] DebugReset 완료");
        }

        /// <summary>
        /// Editor 디버그 강제 언락. UnlockTracker 의 평가 로직 우회 — 다음 게임 풀 등장 검증용.
        /// PlayerPrefs 즉시 저장 + UnlockSetSync.PushSelf 로 멀티 측에도 즉시 반영.
        /// </summary>
        public void DebugForceUnlock(UnlockableType type, int id)
        {
            bool changed = false;
            switch (type)
            {
                case UnlockableType.Skill:
                    changed = unlockedSkillIds.Add(id);
                    break;
                case UnlockableType.Character:
                    changed = unlockedCharacterIds.Add(id);
                    break;
                case UnlockableType.Weapon:
                    // weaponId 는 string — DB 에서 id(=index) → weaponId 변환.
                    var db = GameManager.Instance?.WeaponDB;
                    if (db != null && db.All != null && id >= 0 && id < db.All.Count)
                    {
                        var w = db.All[id];
                        if (w != null && !string.IsNullOrEmpty(w.weaponId))
                            changed = unlockedWeaponIds.Add(w.weaponId);
                    }
                    break;
                case UnlockableType.RefreshCharge:
                    changed = unlockedBonusIndices.Add(id);
                    break;
            }
            if (changed)
            {
                SaveUnlockSets();
                UnlockSetSync.PushSelf();
            }
        }
    }
}
