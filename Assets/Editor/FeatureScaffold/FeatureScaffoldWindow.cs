#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace ProjectSD.EditorTools.FeatureScaffold
{
    public sealed class FeatureScaffoldWindow : EditorWindow
    {
        private const string SharedAssemblyName = "Shared";
        private const string FeaturesRootPath = "Assets/Scripts_/Features";

        private string _featureNameInput = string.Empty;

        [MenuItem("Tools/Feature Scaffold/Create Feature...", priority = 1200)]
        private static void OpenWindow()
        {
            var window = GetWindow<FeatureScaffoldWindow>("Feature Scaffold");
            window.minSize = new Vector2(420f, 240f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Create Feature Scaffold", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Creates Feature folders, asmdef files with dependency rules, and minimal starter files.",
                MessageType.Info);

            EditorGUILayout.Space();
            _featureNameInput = EditorGUILayout.TextField("Feature Name", _featureNameInput);

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Create Scaffold", GUILayout.Height(30f)))
            {
                CreateScaffold(_featureNameInput);
            }
        }

        private static void CreateScaffold(string rawFeatureName)
        {
            if (!TryNormalizeFeatureName(rawFeatureName, out var featureName, out var validationError))
            {
                EditorUtility.DisplayDialog("Feature Scaffold", validationError, "OK");
                return;
            }

            var created = new List<string>();
            var skipped = new List<string>();

            var featureRoot = CombineUnityPath(FeaturesRootPath, featureName);
            var domainPath = CombineUnityPath(featureRoot, "Domain");
            var applicationPath = CombineUnityPath(featureRoot, "Application");
            var portsPath = CombineUnityPath(applicationPath, "Ports");
            var presentationPath = CombineUnityPath(featureRoot, "Presentation");
            var infrastructurePath = CombineUnityPath(featureRoot, "Infrastructure");
            var bootstrapPath = CombineUnityPath(featureRoot, "Bootstrap");

            EnsureDirectory(featureRoot, created, skipped);
            EnsureDirectory(domainPath, created, skipped);
            EnsureDirectory(applicationPath, created, skipped);
            EnsureDirectory(portsPath, created, skipped);
            EnsureDirectory(presentationPath, created, skipped);
            EnsureDirectory(infrastructurePath, created, skipped);
            EnsureDirectory(bootstrapPath, created, skipped);

            var domainAssembly = "Features." + featureName + ".Domain";
            var applicationAssembly = "Features." + featureName + ".Application";
            var presentationAssembly = "Features." + featureName + ".Presentation";
            var infrastructureAssembly = "Features." + featureName + ".Infrastructure";
            var bootstrapAssembly = "Features." + featureName + ".Bootstrap";

            CreateAsmdef(
                CombineUnityPath(domainPath, domainAssembly + ".asmdef"),
                new AsmdefData
                {
                    name = domainAssembly,
                    references = new[] { SharedAssemblyName },
                    noEngineReferences = true
                },
                created,
                skipped);

            CreateAsmdef(
                CombineUnityPath(applicationPath, applicationAssembly + ".asmdef"),
                new AsmdefData
                {
                    name = applicationAssembly,
                    references = new[] { SharedAssemblyName, domainAssembly },
                    noEngineReferences = false
                },
                created,
                skipped);

            CreateAsmdef(
                CombineUnityPath(presentationPath, presentationAssembly + ".asmdef"),
                new AsmdefData
                {
                    name = presentationAssembly,
                    references = new[] { SharedAssemblyName, applicationAssembly, domainAssembly },
                    noEngineReferences = false
                },
                created,
                skipped);

            CreateAsmdef(
                CombineUnityPath(infrastructurePath, infrastructureAssembly + ".asmdef"),
                new AsmdefData
                {
                    name = infrastructureAssembly,
                    references = new[] { SharedAssemblyName, applicationAssembly, domainAssembly },
                    noEngineReferences = false
                },
                created,
                skipped);

            CreateAsmdef(
                CombineUnityPath(bootstrapPath, bootstrapAssembly + ".asmdef"),
                new AsmdefData
                {
                    name = bootstrapAssembly,
                    references = new[] { SharedAssemblyName, applicationAssembly, domainAssembly, presentationAssembly, infrastructureAssembly },
                    noEngineReferences = false
                },
                created,
                skipped);

            CreateFileIfMissing(
                CombineUnityPath(portsPath, "I" + featureName + "OutputPort.cs"),
                BuildOutputPortTemplate(featureName),
                created,
                skipped);

            CreateFileIfMissing(
                CombineUnityPath(presentationPath, featureName + "Presenter.cs"),
                BuildPresenterTemplate(featureName),
                created,
                skipped);

            CreateFileIfMissing(
                CombineUnityPath(bootstrapPath, featureName + "Bootstrap.cs"),
                BuildBootstrapTemplate(featureName),
                created,
                skipped);

            AssetDatabase.Refresh();

            var summary = "Feature: " + featureName + "\n"
                + "Created: " + created.Count + "\n"
                + "Skipped: " + skipped.Count;
            EditorUtility.DisplayDialog("Feature Scaffold", summary, "OK");

            if (created.Count > 0)
            {
                Debug.Log("[FeatureScaffold] Created:\n- " + string.Join("\n- ", created));
            }

            if (skipped.Count > 0)
            {
                Debug.Log("[FeatureScaffold] Skipped (already exists):\n- " + string.Join("\n- ", skipped));
            }

            var featureObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(featureRoot);
            if (featureObject != null)
            {
                Selection.activeObject = featureObject;
                EditorGUIUtility.PingObject(featureObject);
            }
        }

        private static bool TryNormalizeFeatureName(string raw, out string normalized, out string error)
        {
            normalized = string.Empty;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(raw))
            {
                error = "Feature name is required.";
                return false;
            }

            var matches = Regex.Matches(raw.Trim(), "[A-Za-z0-9]+");
            if (matches.Count == 0)
            {
                error = "Use letters and numbers for the feature name.";
                return false;
            }

            var builder = new StringBuilder();
            for (var i = 0; i < matches.Count; i++)
            {
                var part = matches[i].Value;
                if (part.Length == 0)
                {
                    continue;
                }

                builder.Append(char.ToUpperInvariant(part[0]));
                if (part.Length > 1)
                {
                    builder.Append(part.Substring(1));
                }
            }

            normalized = builder.ToString();
            if (normalized.Length == 0 || !char.IsLetter(normalized[0]))
            {
                error = "Feature name must start with a letter.";
                return false;
            }

            return true;
        }

        private static void EnsureDirectory(string path, List<string> created, List<string> skipped)
        {
            var systemPath = ToSystemPath(path);
            if (Directory.Exists(systemPath))
            {
                skipped.Add(path);
                return;
            }

            Directory.CreateDirectory(systemPath);
            created.Add(path);
        }

        private static void CreateAsmdef(string path, AsmdefData data, List<string> created, List<string> skipped)
        {
            var json = JsonUtility.ToJson(data, true);
            CreateFileIfMissing(path, json + "\n", created, skipped);
        }

        private static void CreateFileIfMissing(string path, string content, List<string> created, List<string> skipped)
        {
            var systemPath = ToSystemPath(path);
            if (File.Exists(systemPath))
            {
                skipped.Add(path);
                return;
            }

            var directory = Path.GetDirectoryName(systemPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(systemPath, content);
            created.Add(path);
        }

        private static string BuildOutputPortTemplate(string featureName)
        {
            return "namespace Features." + featureName + ".Application.Ports\n"
                + "{\n"
                + "    public interface I" + featureName + "OutputPort\n"
                + "    {\n"
                + "    }\n"
                + "}\n";
        }

        private static string BuildBootstrapTemplate(string featureName)
        {
            return "using UnityEngine;\n\n"
                + "namespace Features." + featureName + ".Bootstrap\n"
                + "{\n"
                + "    public sealed class " + featureName + "Bootstrap : MonoBehaviour\n"
                + "    {\n"
                + "        private void Awake()\n"
                + "        {\n"
                + "        }\n"
                + "    }\n"
                + "}\n";
        }

        private static string BuildPresenterTemplate(string featureName)
        {
            return "namespace Features." + featureName + ".Presentation\n"
                + "{\n"
                + "    public sealed class " + featureName + "Presenter\n"
                + "    {\n"
                + "    }\n"
                + "}\n";
        }

        private static string CombineUnityPath(string left, string right)
        {
            if (string.IsNullOrEmpty(left))
            {
                return right.Replace("\\", "/");
            }

            if (string.IsNullOrEmpty(right))
            {
                return left.Replace("\\", "/");
            }

            return (left.TrimEnd('/', '\\') + "/" + right.TrimStart('/', '\\')).Replace("\\", "/");
        }

        private static string ToSystemPath(string unityPath)
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(root, unityPath.Replace("/", Path.DirectorySeparatorChar.ToString()));
        }

        [Serializable]
        private sealed class AsmdefData
        {
            public string name;
            public string[] references;
            public string[] includePlatforms = Array.Empty<string>();
            public string[] excludePlatforms = Array.Empty<string>();
            public bool allowUnsafeCode = false;
            public bool overrideReferences = false;
            public string[] precompiledReferences = Array.Empty<string>();
            public bool autoReferenced = true;
            public string[] defineConstraints = Array.Empty<string>();
            public VersionDefine[] versionDefines = Array.Empty<VersionDefine>();
            public bool noEngineReferences;
        }

        [Serializable]
        private struct VersionDefine
        {
            public string name;
            public string expression;
            public string define;
        }
    }
}
#endif
