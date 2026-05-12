using UnityEngine;
using UnityEditor;
using SwDreams.Features.Unlock.Adapter;
using SwDreams.Features.Unlock.Domain;
using SwDreams.Shared.Platform.Adapter;
using SwDreams.Shared.Platform.Domain;

namespace SwDreams.Editor
{
    /// <summary>
    /// 메타 언락 시스템 디버그 / 테스트 도구.
    ///
    /// 메뉴: Tools > Meta Unlock Debug
    ///
    /// 기능:
    /// - 누적 통계 / 언락 셋 / RefreshCharge 보너스 실시간 표시
    /// - Reset All (PlayerPrefs 모든 meta.* 키 삭제 + 메모리 캐시 비우기)
    /// - 임의 스킬/캐릭터 ID 강제 언락 (테스트 풀 등장 검증용)
    /// - PushSelf 강제 호출 (멀티 동기화 디버그)
    /// - 멀티 검증용 IsSkillUnlocked 상세 로그 토글
    /// </summary>
    public class MetaUnlockDebugWindow : EditorWindow
    {
        private int testSkillId;
        private int testCharacterId;
        private string testWeaponId = "";
        private Vector2 scroll;
        private bool foldoutStats = true;
        private bool foldoutUnlocks = true;
        private bool foldoutActions = true;

        [MenuItem("Tools/Meta Unlock Debug")]
        public static void Open()
        {
            var w = GetWindow<MetaUnlockDebugWindow>("Meta Unlock Debug");
            w.minSize = new Vector2(360, 480);
            w.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += Repaint;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Repaint;
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.LabelField("Meta Unlock Debug", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Play 모드에서만 인스턴스가 살아있음 (MetaProgressStore / UnlockTracker).\n" +
                "Play 모드 아닐 때도 PlayerPrefs 직접 삭제는 가능.",
                MessageType.Info);

            EditorGUILayout.Space();

            DrawStatsSection();
            EditorGUILayout.Space();
            DrawUnlocksSection();
            EditorGUILayout.Space();
            DrawActionsSection();

            EditorGUILayout.EndScrollView();
        }

        // ===== 누적 통계 =====
        private void DrawStatsSection()
        {
            foldoutStats = EditorGUILayout.Foldout(foldoutStats, "누적 통계 (MetaProgressStore)", true);
            if (!foldoutStats) return;

            var store = MetaProgressStore.Instance;
            if (store == null)
            {
                EditorGUILayout.HelpBox("MetaProgressStore.Instance 없음 (Play 모드 진입 후 표시).", MessageType.None);
                return;
            }

            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Total Kills",   store.TotalKills.ToString());
            EditorGUILayout.LabelField("Total Deaths",  store.TotalDeaths.ToString());
            EditorGUILayout.LabelField("Total Runs",    store.TotalRuns.ToString());
            EditorGUILayout.LabelField("Total Clears",  store.TotalClears.ToString());
            EditorGUILayout.LabelField("Bosses Defeated", "[" + string.Join(",", store.BossDefeatedIds) + "]");
            EditorGUILayout.LabelField("Zones Visited",   "[" + string.Join(",", store.ZonesVisitedIds) + "]");
            EditorGUILayout.LabelField("Death By Enemy",  "[" + string.Join(",", store.DeathByEnemyIds) + "]");
            EditorGUI.indentLevel--;
        }

        // ===== 언락 셋 =====
        private void DrawUnlocksSection()
        {
            foldoutUnlocks = EditorGUILayout.Foldout(foldoutUnlocks, "언락 셋 (UnlockTracker)", true);
            if (!foldoutUnlocks) return;

            var t = UnlockTracker.Instance;
            if (t == null)
            {
                EditorGUILayout.HelpBox("UnlockTracker.Instance 없음 (Play 모드 진입 후 표시).", MessageType.None);
                return;
            }

            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Unlocked Skills",     "[" + string.Join(",", t.UnlockedSkillIds) + "]");
            EditorGUILayout.LabelField("Unlocked Weapons",    "[" + string.Join(",", t.UnlockedWeaponIds) + "]");
            EditorGUILayout.LabelField("Unlocked Characters", "[" + string.Join(",", t.UnlockedCharacterIds) + "]");
            EditorGUILayout.LabelField("Refresh Bonus",       "+" + t.BonusRefreshCharges);
            EditorGUI.indentLevel--;
        }

        // ===== 액션 =====
        private void DrawActionsSection()
        {
            foldoutActions = EditorGUILayout.Foldout(foldoutActions, "액션", true);
            if (!foldoutActions) return;

            EditorGUILayout.LabelField("초기화", EditorStyles.miniBoldLabel);

            // Reset All
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("🗑 Reset All (PlayerPrefs + 메모리)"))
                {
                    if (EditorUtility.DisplayDialog("Reset Meta Unlock",
                        "모든 누적 통계 + 언락 셋을 0 으로 리셋하고 PlayerPrefs 의 meta.* / platform.* 키를 삭제합니다.\n계속?",
                        "Reset", "Cancel"))
                    {
                        ResetAll();
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reset Meta Progress Only"))
                {
                    MetaProgressStore.Instance?.DebugReset();
                }
                if (GUILayout.Button("Reset Unlocks Only"))
                {
                    UnlockTracker.Instance?.DebugReset();
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("강제 언락 (테스트 풀 등장 검증용)", EditorStyles.miniBoldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                testSkillId = EditorGUILayout.IntField("Skill ID", testSkillId);
                if (GUILayout.Button("Force Unlock", GUILayout.Width(110)))
                {
                    ForceUnlockSkill(testSkillId);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                testCharacterId = EditorGUILayout.IntField("Character ID", testCharacterId);
                if (GUILayout.Button("Force Unlock", GUILayout.Width(110)))
                {
                    ForceUnlockCharacter(testCharacterId);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("멀티플레이 동기화 디버그", EditorStyles.miniBoldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("PushSelf (CustomProperties 강제 갱신)"))
                {
                    UnlockSetSync.PushSelf();
                }
            }

            UnlockSetSync.verboseLogging = EditorGUILayout.Toggle(
                new GUIContent("Verbose IsUnlocked Logging",
                    "true 면 IsSkillUnlocked 호출 시 actor/skillId/CustomProperties 상세 로그. " +
                    "멀티 D5 race 디버깅용. 평소 false."),
                UnlockSetSync.verboseLogging);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("콘솔 Dump", EditorStyles.miniBoldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Print Meta Progress"))
                    MetaProgressStore.Instance?.DebugPrint();
                if (GUILayout.Button("Print Unlocks"))
                    UnlockTracker.Instance?.DebugPrint();
            }
        }

        // ===== 액션 구현 =====

        private void ResetAll()
        {
            // PlayerPrefs 직접 삭제 (Play 모드 아니어도 동작).
            // ParrelSync clone 별 prefix 가 적용되도록 LocalPlatformService.Make*Key 사용 —
            // 자기 인스턴스의 키만 삭제 (다른 clone 의 키에 영향 X).
            string[] dataKeys = {
                "meta.run_stats",
                "meta.unlocked_skills",
                "meta.unlocked_weapons",
                "meta.unlocked_characters",
                "meta.unlocked_bonuses",
            };
            string[] statKeys = {
                AchievementId.Stat_TotalKills,
                AchievementId.Stat_TotalDeaths,
                AchievementId.Stat_TotalRuns,
                AchievementId.Stat_TotalClears,
            };

            int deleted = 0;
            foreach (var k in dataKeys)
            {
                string full = LocalPlatformService.MakeDataKey(k);
                if (PlayerPrefs.HasKey(full)) { PlayerPrefs.DeleteKey(full); deleted++; }
            }
            foreach (var k in statKeys)
            {
                string full = LocalPlatformService.MakeStatKey(k);
                if (PlayerPrefs.HasKey(full)) { PlayerPrefs.DeleteKey(full); deleted++; }
            }
            PlayerPrefs.Save();

            // 메모리 캐시도 동기 리셋 (Play 모드면)
            MetaProgressStore.Instance?.DebugReset();
            UnlockTracker.Instance?.DebugReset();

            Debug.Log($"[MetaUnlockDebug] Reset All — PlayerPrefs {deleted} 키 삭제 + 메모리 캐시 리셋 완료");
        }

        private void ForceUnlockSkill(int skillId)
        {
            // Editor 강제 — UnlockTracker 의 영구 저장 우회. 다음 게임부터 풀에 등장.
            // 평가 로직과 별개로 직접 셋 조작 — 디버그 전용.
            var t = UnlockTracker.Instance;
            if (t == null)
            {
                Debug.LogWarning("[MetaUnlockDebug] UnlockTracker.Instance 없음 (Play 모드 진입 후 가능).");
                return;
            }
            t.DebugForceUnlock(UnlockableType.Skill, skillId);
            Debug.Log($"[MetaUnlockDebug] Force unlock skill {skillId}");
        }

        private void ForceUnlockCharacter(int characterId)
        {
            var t = UnlockTracker.Instance;
            if (t == null)
            {
                Debug.LogWarning("[MetaUnlockDebug] UnlockTracker.Instance 없음.");
                return;
            }
            t.DebugForceUnlock(UnlockableType.Character, characterId);
            Debug.Log($"[MetaUnlockDebug] Force unlock character {characterId}");
        }
    }
}
