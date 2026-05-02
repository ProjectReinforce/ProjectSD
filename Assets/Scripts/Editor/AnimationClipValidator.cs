using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SwDreams.EditorTools
{
    /// <summary>
    /// AnimationClip 의 Sprite curve reference 가 살아있는지 일괄 스캔.
    /// 깨진 sprite (m_Sprite curve 의 ObjectReferenceKey.value == null) 를 찾아 보고.
    ///
    /// 사용:
    ///   1. Project 창에서 검사할 폴더 선택 (예: Assets/FromStore/sanctum_pixel)
    ///   2. 메뉴 Tools → Validate AnimationClip Sprites (Selected Folder)
    ///   3. 또는 Tools → Validate AnimationClip Sprites (All) 로 전체 스캔
    ///   4. Console 의 경고 / 결과 텍스트 확인
    ///
    /// 깨진 클립을 발견하면 그 .anim 의 path + frame time + 바인딩 인덱스를 출력.
    /// 후속: 깨진 클립 삭제 또는 sprite 재할당 (Animation 창에서 수동).
    /// </summary>
    public static class AnimationClipValidator
    {
        private const string SpritePropertyName = "m_Sprite";

        [MenuItem("Tools/Validate AnimationClip Sprites (Selected Folder)")]
        public static void ValidateSelectedFolder()
        {
            string folder = GetSelectedFolderPath();
            if (string.IsNullOrEmpty(folder))
            {
                Debug.LogWarning("[AnimationClipValidator] Project 창에서 폴더를 선택하세요.");
                return;
            }

            Validate(new[] { folder });
        }

        [MenuItem("Tools/Validate AnimationClip Sprites (All)")]
        public static void ValidateAll()
        {
            Validate(null);
        }

        private static void Validate(string[] searchInFolders)
        {
            var guids = searchInFolders == null
                ? AssetDatabase.FindAssets("t:AnimationClip")
                : AssetDatabase.FindAssets("t:AnimationClip", searchInFolders);

            int totalClips = 0;
            int brokenClips = 0;
            int totalBrokenFrames = 0;
            var brokenList = new List<string>();

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null) continue;

                totalClips++;

                var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
                int clipBrokenFrames = 0;
                var perBindingDetails = new StringBuilder();

                foreach (var binding in bindings)
                {
                    if (binding.propertyName != SpritePropertyName) continue;

                    var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                    if (keys == null) continue;

                    for (int i = 0; i < keys.Length; i++)
                    {
                        if (keys[i].value == null)
                        {
                            clipBrokenFrames++;
                            perBindingDetails.AppendLine(
                                $"    - binding[path='{binding.path}'] frame@{keys[i].time:F3}s (key index {i})");
                        }
                    }
                }

                if (clipBrokenFrames > 0)
                {
                    brokenClips++;
                    totalBrokenFrames += clipBrokenFrames;
                    string entry = $"[{path}] 깨진 frame {clipBrokenFrames}개\n{perBindingDetails}";
                    brokenList.Add(entry);
                    Debug.LogWarning(entry, clip);
                }
            }

            // 요약 보고
            var summary = new StringBuilder();
            summary.AppendLine("=== AnimationClipValidator 결과 ===");
            summary.AppendLine($"검사 대상: {(searchInFolders == null ? "전체 프로젝트" : string.Join(", ", searchInFolders))}");
            summary.AppendLine($"검사한 AnimationClip: {totalClips}개");
            summary.AppendLine($"깨진 클립: {brokenClips}개");
            summary.AppendLine($"깨진 frame 합계: {totalBrokenFrames}개");

            if (brokenClips == 0)
            {
                summary.AppendLine("✅ 모든 클립의 sprite reference 정상.");
                Debug.Log(summary.ToString());
            }
            else
            {
                summary.AppendLine("⚠ 깨진 클립 발견 — 위 로그 참조 (각 entry 클릭 시 .anim 으로 핑).");
                summary.AppendLine("후속 옵션:");
                summary.AppendLine("  (1) Animation 창에서 수동으로 sprite 재할당");
                summary.AppendLine("  (2) 깨진 클립 사용 안 함 — 다른 캐릭터의 클립으로 Override 대체");
                summary.AppendLine("  (3) sanctum 패키지 재 import (Project 창에서 .png 우클릭 → Reimport)");
                Debug.LogWarning(summary.ToString());
            }
        }

        private static string GetSelectedFolderPath()
        {
            var obj = Selection.activeObject;
            if (obj == null) return null;

            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path)) return null;

            // 파일을 선택했으면 부모 폴더로
            if (System.IO.File.Exists(path))
                path = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');

            if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path))
                return null;

            return path;
        }
    }
}
