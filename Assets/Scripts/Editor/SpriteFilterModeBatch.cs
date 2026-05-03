using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SwDreams.EditorTools
{
    /// <summary>
    /// Project 창에서 선택한 폴더(들) 하위의 모든 PNG 텍스처에
    /// FilterMode=Point + Compression=None 을 일괄 적용 (픽셀아트용).
    ///
    /// 사용:
    ///   1. Project 창에서 폴더 1개 이상 선택 (다중 선택 가능)
    ///   2. 메뉴 Tools → Apply Pixel Art Settings (Selected Folder)
    ///   3. 확인 다이얼로그 → 진행 → Console 요약
    ///
    /// 메모:
    /// - 두 값 모두 이미 목표 상태인 텍스처는 스킵 (불필요한 reimport 회피).
    /// - .png 만 대상. .jpg/.tga 등은 의도적으로 제외.
    /// - SaveAndReimport 가 무거우므로 StartAssetEditing/StopAssetEditing 으로 batch.
    /// </summary>
    public static class SpriteFilterModeBatch
    {
        [MenuItem("Tools/Apply Pixel Art Settings (Selected Folder)", true)]
        private static bool ValidateSelected() => GetSelectedFolders().Count > 0;

        [MenuItem("Tools/Apply Pixel Art Settings (Selected Folder)")]
        private static void ApplySelected()
        {
            var folders = GetSelectedFolders();
            if (folders.Count == 0)
            {
                Debug.LogWarning("[SpriteFilterModeBatch] Project 창에서 폴더를 1개 이상 선택하세요.");
                return;
            }

            string folderList = string.Join("\n  • ", folders);
            bool ok = EditorUtility.DisplayDialog(
                "Apply Pixel Art Settings",
                $"아래 폴더 하위의 모든 .png 에 다음을 적용합니다:\n\n" +
                $"  • Filter Mode = Point\n" +
                $"  • Compression = None\n\n" +
                $"대상:\n  • {folderList}\n\n진행할까요?",
                "진행",
                "취소");
            if (!ok) return;

            Apply(folders.ToArray());
        }

        private static void Apply(string[] searchInFolders)
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", searchInFolders);

            int scanned = 0;
            int changed = 0;
            int skippedAlready = 0;
            int skippedNonPng = 0;
            int failed = 0;
            var changedList = new List<string>();

            try
            {
                AssetDatabase.StartAssetEditing();

                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (string.IsNullOrEmpty(path)) continue;

                    if (!path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
                    {
                        skippedNonPng++;
                        continue;
                    }

                    scanned++;

                    if (EditorUtility.DisplayCancelableProgressBar(
                            "Apply Pixel Art Settings",
                            $"{i + 1}/{guids.Length}  {path}",
                            (float)(i + 1) / guids.Length))
                    {
                        Debug.LogWarning("[SpriteFilterModeBatch] 사용자 취소 — 여기까지 변경분만 저장.");
                        break;
                    }

                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null)
                    {
                        failed++;
                        Debug.LogWarning($"[SpriteFilterModeBatch] TextureImporter 아님 — 스킵: {path}");
                        continue;
                    }

                    bool needsFilter = importer.filterMode != FilterMode.Point;
                    bool needsCompression = importer.textureCompression != TextureImporterCompression.Uncompressed;

                    if (!needsFilter && !needsCompression)
                    {
                        skippedAlready++;
                        continue;
                    }

                    if (needsFilter) importer.filterMode = FilterMode.Point;
                    if (needsCompression) importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                    changed++;
                    changedList.Add(path);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            var summary = new StringBuilder();
            summary.AppendLine("=== Apply Pixel Art Settings 결과 ===");
            summary.AppendLine($"대상 폴더: {string.Join(", ", searchInFolders)}");
            summary.AppendLine($"목표: Filter=Point, Compression=None");
            summary.AppendLine($"스캔한 .png: {scanned}개  (비-PNG 스킵: {skippedNonPng}개)");
            summary.AppendLine($"변경됨: {changed}개");
            summary.AppendLine($"이미 일치해서 스킵: {skippedAlready}개");
            if (failed > 0) summary.AppendLine($"실패(스킵): {failed}개");

            if (changed > 0)
            {
                summary.AppendLine("--- 변경 목록 ---");
                foreach (var p in changedList) summary.AppendLine($"  {p}");
            }

            Debug.Log(summary.ToString());
        }

        private static List<string> GetSelectedFolders()
        {
            var result = new List<string>();
            var seen = new HashSet<string>();

            foreach (var obj in Selection.objects)
            {
                if (obj == null) continue;
                string path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path)) continue;

                if (System.IO.File.Exists(path))
                    path = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');

                if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path))
                    continue;

                if (seen.Add(path)) result.Add(path);
            }

            return result;
        }
    }
}
