using UnityEditor;
using UnityEngine;
using System.IO;
using System.Text.RegularExpressions;

namespace Editor
{
    [InitializeOnLoad]
    public static class LayerDependencyValidator
    {
        private static readonly Regex UsingRegex =
            new Regex(@"^\s*using\s+(?:static\s+)?(?:\w+\s*=\s*)?([\w.]+)\s*;");

        private static readonly Regex ForbiddenLayerRegex =
            new Regex(@"^Features\.\w+\.(\w+)");

        static LayerDependencyValidator()
        {
            Validate(silent: true);
        }

        [MenuItem("Tools/Validate Layer Dependencies")]
        public static void ValidateFromMenu()
        {
            Validate(silent: false);
        }

        private static void Validate(bool silent)
        {
            var scriptsRoot = Path.Combine(Application.dataPath, "Scripts_");
            if (!Directory.Exists(scriptsRoot)) return;

            var csFiles = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories);
            var violationCount = 0;

            foreach (var file in csFiles)
            {
                var normalizedPath = file.Replace("\\", "/");
                var layer = DetectLayer(normalizedPath);
                if (layer == null) continue;

                var lines = File.ReadAllLines(file);

                for (var i = 0; i < lines.Length; i++)
                {
                    var match = UsingRegex.Match(lines[i]);
                    if (!match.Success) continue;

                    var ns = match.Groups[1].Value;
                    var violation = Check(ns, layer);
                    if (violation == null) continue;

                    var shortPath = normalizedPath.Substring(normalizedPath.IndexOf("Assets/"));
                    Debug.LogError($"[Layer Rule] {shortPath}:{i + 1} — {violation}\n  using {ns};");
                    violationCount++;
                }
            }

            if (violationCount > 0)
                Debug.LogError($"[Layer Rule] {violationCount} violation(s) found.");
            else if (!silent)
                Debug.Log("[Layer Rule] No violations found.");
        }

        private static string DetectLayer(string path)
        {
            if (Contains(path, "/Shared/")) return "Shared";
            if (Contains(path, "/Bootstrap/")) return "Bootstrap";
            if (Contains(path, "/Infrastructure/")) return "Infrastructure";
            if (Contains(path, "/Presentation/")) return "Presentation";
            if (Contains(path, "/Application/")) return "Application";
            if (Contains(path, "/Domain/")) return "Domain";
            return null;
        }

        private static string Check(string ns, string layer)
        {
            // 레이어 방향 규칙만 강제한다.
            // 크로스 피처 참조는 허용 — 피처 간 통신은 이벤트/포트로 하되, 컴파일 수준에서는 막지 않는다.
            switch (layer)
            {
                case "Domain":
                    if (ns.StartsWith("UnityEngine")) return "Domain → UnityEngine 금지";
                    if (ns.StartsWith("UnityEditor")) return "Domain → UnityEditor 금지";
                    if (ns.StartsWith("Photon")) return "Domain → Photon 금지";
                    if (ns.StartsWith("System.IO")) return "Domain → System.IO 금지";
                    if (CheckForbiddenLayer(ns, "Application")) return "Domain → Application 참조 금지";
                    if (CheckForbiddenLayer(ns, "Presentation")) return "Domain → Presentation 참조 금지";
                    if (CheckForbiddenLayer(ns, "Infrastructure")) return "Domain → Infrastructure 참조 금지";
                    if (CheckForbiddenLayer(ns, "Bootstrap")) return "Domain → Bootstrap 참조 금지";
                    break;

                case "Application":
                    if (ns.StartsWith("UnityEngine")) return "Application → UnityEngine 금지";
                    if (ns.StartsWith("UnityEditor")) return "Application → UnityEditor 금지";
                    if (ns.StartsWith("Photon")) return "Application → Photon 금지";
                    if (CheckForbiddenLayer(ns, "Presentation")) return "Application → Presentation 참조 금지";
                    if (CheckForbiddenLayer(ns, "Infrastructure")) return "Application → Infrastructure 참조 금지";
                    if (CheckForbiddenLayer(ns, "Bootstrap")) return "Application → Bootstrap 참조 금지";
                    break;

                case "Presentation":
                    if (ns.StartsWith("Photon")) return "Presentation → Photon 금지";
                    if (CheckForbiddenLayer(ns, "Infrastructure")) return "Presentation → Infrastructure 참조 금지";
                    if (CheckForbiddenLayer(ns, "Bootstrap")) return "Presentation → Bootstrap 참조 금지";
                    break;

                case "Infrastructure":
                    if (CheckForbiddenLayer(ns, "Presentation")) return "Infrastructure → Presentation 참조 금지";
                    if (CheckForbiddenLayer(ns, "Bootstrap")) return "Infrastructure → Bootstrap 참조 금지";
                    break;

                case "Bootstrap":
                    break; // Bootstrap은 제한 없음 (wiring layer)

                case "Shared":
                    if (ns.StartsWith("Features.")) return "Shared → Features 참조 금지";
                    break;
            }

            return null;
        }

        private static bool CheckForbiddenLayer(string ns, string forbiddenLayer)
        {
            // Features.*.Presentation, Features.*.Infrastructure 등을 감지
            if (!ns.StartsWith("Features.")) return false;
            var match = ForbiddenLayerRegex.Match(ns);
            return match.Success && match.Groups[1].Value == forbiddenLayer;
        }

        private static bool Contains(string path, string segment)
        {
            return path.IndexOf(segment, System.StringComparison.Ordinal) >= 0;
        }
    }
}
