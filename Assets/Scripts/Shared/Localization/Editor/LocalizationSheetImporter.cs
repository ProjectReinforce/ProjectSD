using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using SwDreams.Shared.Localization.Adapter;

namespace SwDreams.Shared.Localization.EditorTools
{
    /// <summary>
    /// Google Sheet (public read) → CSV → LocalizationTable.asset 임포터.
    ///
    /// 시트 포맷 (첫 행 헤더, 본문은 2행~):
    ///   key, ko_auto, en_auto, ja_auto, zh_auto, ko_final, en_final, ja_final, zh_final, note
    /// 컬럼 인덱스: 0=key, 5=ko_final, 6=en_final, 7=ja_final, 8=zh_final, 9=note.
    /// 게임은 *_final 만 읽음 — *_auto 는 검수 워크플로용.
    ///
    /// EditorPrefs 에 Sheet ID/GID 저장 (머신 단위, git 미반영).
    /// </summary>
    public class LocalizationSheetImporter : EditorWindow
    {
        private const string PrefKey_SheetId = "ProjectSD.Localization.SheetId";
        private const string PrefKey_Gid = "ProjectSD.Localization.Gid";

        // CSV 컬럼 인덱스
        private const int Col_Key = 0;
        private const int Col_KoFinal = 5;
        private const int Col_EnFinal = 6;
        private const int Col_JaFinal = 7;
        private const int Col_ZhFinal = 8;
        private const int Col_Note = 9;

        private string sheetId;
        private string gid;
        private LocalizationTable targetTable;
        private bool isImporting;

        [MenuItem("ProjectSD/Localization/Import from Google Sheet")]
        public static void Open() => GetWindow<LocalizationSheetImporter>("Localization Importer");

        private void OnEnable()
        {
            sheetId = EditorPrefs.GetString(PrefKey_SheetId, "");
            gid = EditorPrefs.GetString(PrefKey_Gid, "0");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Google Sheet", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "URL: https://docs.google.com/spreadsheets/d/{SheetId}/edit#gid={Gid}\n" +
                "공유 설정: '링크가 있는 모든 사용자 (보기 가능)'.",
                MessageType.Info);

            sheetId = EditorGUILayout.TextField("Sheet ID", sheetId);
            gid = EditorGUILayout.TextField("GID (tab id)", gid);
            targetTable = (LocalizationTable)EditorGUILayout.ObjectField(
                "Target Table", targetTable, typeof(LocalizationTable), false);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(isImporting))
            {
                if (GUILayout.Button("Pull & Import"))
                {
                    EditorPrefs.SetString(PrefKey_SheetId, sheetId);
                    EditorPrefs.SetString(PrefKey_Gid, gid);
                    _ = PullAndImport();
                }
            }

            if (isImporting) EditorGUILayout.HelpBox("Importing...", MessageType.None);
        }

        private async Task PullAndImport()
        {
            if (string.IsNullOrWhiteSpace(sheetId) || targetTable == null)
            {
                EditorUtility.DisplayDialog("Localization Importer",
                    "Sheet ID 또는 Target Table 미설정.", "OK");
                return;
            }

            isImporting = true;
            Repaint();

            // cache-buster: Google Sheets CSV export 가 CDN 에 잠시 캐시될 수 있음.
            long bust = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string url = $"https://docs.google.com/spreadsheets/d/{sheetId}/export?format=csv&gid={gid}&_cb={bust}";
            string csv;
            try
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
                csv = await http.GetStringAsync(url);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Localization] CSV 다운로드 실패: {ex.Message}");
                EditorUtility.DisplayDialog("Localization Importer",
                    $"다운로드 실패:\n{ex.Message}\n\n시트 공유 설정(보기 가능) 확인 필요.", "OK");
                isImporting = false;
                Repaint();
                return;
            }

            var rows = ParseCsv(csv);
            int imported = ImportRows(rows);
            EditorUtility.SetDirty(targetTable);
            AssetDatabase.SaveAssets();

            isImporting = false;
            Repaint();

            Debug.Log($"[Localization] Imported {imported} keys → {targetTable.name}");
            EditorUtility.DisplayDialog("Localization Importer",
                $"임포트 완료\n\n키 {imported} 개 → {targetTable.name}", "OK");
        }

        private int ImportRows(List<List<string>> rows)
        {
            if (rows.Count < 2)
            {
                Debug.LogWarning("[Localization] 시트가 비었거나 헤더만 있음.");
                return 0;
            }

            // 헤더 약식 검증: 첫 컬럼이 "key" 인지.
            if (rows[0].Count == 0 || rows[0][Col_Key].Trim().ToLowerInvariant() != "key")
                Debug.LogWarning($"[Localization] 헤더 첫 컬럼이 'key' 가 아님: '{(rows[0].Count > 0 ? rows[0][0] : "")}'. 임포트는 진행.");

            var entries = targetTable.EditorEntries;
            entries.Clear();

            var seen = new HashSet<string>();
            for (int i = 1; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row.Count <= Col_Key) continue;

                string key = row[Col_Key].Trim();
                if (string.IsNullOrEmpty(key)) continue;

                if (!seen.Add(key))
                {
                    Debug.LogWarning($"[Localization] 중복 키 (행 {i + 1}): {key}");
                    continue;
                }

                entries.Add(new LocalizationTable.Entry
                {
                    key  = key,
                    ko   = row.Count > Col_KoFinal ? row[Col_KoFinal] : "",
                    en   = row.Count > Col_EnFinal ? row[Col_EnFinal] : "",
                    ja   = row.Count > Col_JaFinal ? row[Col_JaFinal] : "",
                    zh   = row.Count > Col_ZhFinal ? row[Col_ZhFinal] : "",
                    note = row.Count > Col_Note    ? row[Col_Note]    : "",
                });
            }
            targetTable.RebuildLookup();
            return entries.Count;
        }

        // RFC 4180 호환 CSV 파서. 따옴표 안 콤마/줄바꿈/이스케이프된 따옴표 처리.
        private static List<List<string>> ParseCsv(string csv)
        {
            var rows = new List<List<string>>();
            var cur = new List<string>();
            var cell = new System.Text.StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < csv.Length; i++)
            {
                char c = csv[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < csv.Length && csv[i + 1] == '"')
                        {
                            cell.Append('"');
                            i++;
                        }
                        else inQuotes = false;
                    }
                    else cell.Append(c);
                }
                else
                {
                    switch (c)
                    {
                        case '"':
                            inQuotes = true;
                            break;
                        case ',':
                            cur.Add(cell.ToString());
                            cell.Clear();
                            break;
                        case '\n':
                            cur.Add(cell.ToString());
                            cell.Clear();
                            rows.Add(cur);
                            cur = new List<string>();
                            break;
                        case '\r':
                            break;
                        default:
                            cell.Append(c);
                            break;
                    }
                }
            }
            if (cell.Length > 0 || cur.Count > 0)
            {
                cur.Add(cell.ToString());
                rows.Add(cur);
            }
            return rows;
        }
    }
}
