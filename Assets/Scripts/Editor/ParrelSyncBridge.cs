using UnityEditor;
using UnityEngine;
using SwDreams.Shared.Platform.Adapter;

namespace SwDreams.Editor
{
    /// <summary>
    /// ParrelSync clone 별 PlayerPrefs 격리 bridge.
    ///
    /// 문제:
    /// - ParrelSync clone 은 원본과 같은 ProjectSettings (CompanyName/ProductName) 를 공유.
    /// - Unity PlayerPrefs 는 CompanyName/ProductName 기준 OS 레지스트리에 저장됨.
    /// - 결과: ParrelSync 양 인스턴스가 **같은 PlayerPrefs namespace 를 공유** →
    ///   메타 언락 멀티 D5 검증이 망가짐 (한쪽 변경이 양쪽에 즉시 반영).
    ///
    /// 해결:
    /// - 본 bridge 가 InitializeOnLoad 시점에 LocalPlatformService.CloneSuffixProvider 셋업.
    /// - 자기가 clone 이면 "clone.<arg>." 같은 prefix 를 모든 PlayerPrefs key 에 prepend.
    /// - 원본 (non-clone) 은 prefix 없이 기존 키. 빌드 환경은 본 어셈블리 자체가 제외됨.
    ///
    /// 사용자가 ParrelSync 미설치 / 비활성 환경이라도 안전 (IsClone=false 면 빈 prefix).
    /// </summary>
    [InitializeOnLoad]
    public static class ParrelSyncBridge
    {
        static ParrelSyncBridge()
        {
            // LocalPlatformService 의 정적 hook 셋업. 호출은 PlayerPrefs key 생성 시마다.
            LocalPlatformService.CloneSuffixProvider = ComputeCloneSuffix;

            // 첫 컴파일/리로드 시 1회 안내 — clone 환경 인지 명확화.
            string suffix = ComputeCloneSuffix();
            if (!string.IsNullOrEmpty(suffix))
                Debug.Log($"[ParrelSyncBridge] Clone 인스턴스 감지 — PlayerPrefs key prefix: \"{suffix}\"");
        }

        private static string ComputeCloneSuffix()
        {
            // ParrelSync 패키지가 Editor-only assembly 라 직접 import 가능 (본 파일도 Editor).
            if (!ParrelSync.ClonesManager.IsClone()) return "";

            string arg = ParrelSync.ClonesManager.GetArgument();
            if (string.IsNullOrEmpty(arg))
                return "clone.";  // arg 미셋업 시 단순 "clone." prefix

            // 안전한 키 문자 만 사용 (영문/숫자/언더스코어).
            string safe = SanitizeKey(arg);
            return $"clone_{safe}.";
        }

        private static string SanitizeKey(string s)
        {
            var sb = new System.Text.StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsLetterOrDigit(c) || c == '_') sb.Append(c);
                else sb.Append('_');
            }
            return sb.ToString();
        }
    }
}
