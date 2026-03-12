#if UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectSD.EditorTools.UnityMcp
{
    [InitializeOnLoad]
    internal static class UnityMcpBridge
    {
        private const int DefaultPort = 51234;
        private const int HealthCheckTimeoutMs = 1000;
        private const string PortConfigRelativePath = "ProjectSettings/UnityMcpPort.txt";
        private const int MaxStoredLogs = 200;
        private const int MaxLogMessageLength = 2000;
        private const int MaxStackTraceLength = 6000;

        private static readonly ConcurrentQueue<Action> MainThreadActions = new ConcurrentQueue<Action>();
        private static readonly object LogLock = new object();
        private static readonly List<ErrorLogEntry> ErrorLogs = new List<ErrorLogEntry>();

        private static HttpListener _listener;
        private static CancellationTokenSource _listenerCts;
        private static int _mainThreadId;

        static UnityMcpBridge()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            EditorApplication.update += DrainMainThreadActions;
            Application.logMessageReceivedThreaded += OnLogMessageReceived;
            AssemblyReloadEvents.beforeAssemblyReload += StopBridge;
            EditorApplication.quitting += StopBridge;
            StartBridge();
        }

        [MenuItem("Tools/Unity MCP/Start Bridge")]
        private static void StartBridgeMenu()
        {
            StartBridge();
        }

        [MenuItem("Tools/Unity MCP/Stop Bridge")]
        private static void StopBridgeMenu()
        {
            StopBridge();
        }

        [MenuItem("Tools/Unity MCP/Print Status")]
        private static void PrintStatusMenu()
        {
            var port = ResolvePort();
            Debug.LogFormat(
                "[Unity MCP] running={0} playing={1} compiling={2} port={3} prefix={4} config={5} scene={6}",
                IsRunning,
                EditorApplication.isPlaying,
                EditorApplication.isCompiling,
                port,
                BuildListenerPrefix(port),
                PortConfigRelativePath,
                SceneManager.GetActiveScene().path);
        }

        private static bool IsRunning
        {
            get { return _listener != null && _listener.IsListening; }
        }

        private static string ProjectRootPath
        {
            get
            {
                var assetsDirectory = Directory.GetParent(Application.dataPath);
                return assetsDirectory != null ? assetsDirectory.FullName : Directory.GetCurrentDirectory();
            }
        }

        private static string PortConfigPath
        {
            get { return Path.Combine(ProjectRootPath, PortConfigRelativePath); }
        }

        private static void StartBridge()
        {
            var port = ResolvePort();
            var listenerPrefix = BuildListenerPrefix(port);

            if (IsRunning)
            {
                Debug.Log("[Unity MCP] Bridge already running at " + listenerPrefix);
                return;
            }

            StopBridge(logWhenAlreadyStopped: false);

            if (IsPortInUse(port))
            {
                if (IsBridgeAlive(listenerPrefix, out var probeDetail))
                {
                    Debug.Log("[Unity MCP] Bridge already alive at " + listenerPrefix + " (" + probeDetail + "). Reusing existing listener.");
                    return;
                }

                Debug.LogError(
                    "[Unity MCP] Cannot start bridge at " + listenerPrefix +
                    " because the port is already in use and /health did not respond. " +
                    "Another process or a stale listener is blocking the port. " +
                    "Close the process using this port or change " + PortConfigRelativePath + ". " +
                    "Probe result: " + probeDetail);
                return;
            }

            try
            {
                _listenerCts = new CancellationTokenSource();
                _listener = new HttpListener();
                _listener.Prefixes.Add(listenerPrefix);
                _listener.Start();
                _ = Task.Run(() => ListenLoopAsync(_listenerCts.Token));
                Debug.Log("[Unity MCP] Bridge started at " + listenerPrefix + " (config: " + PortConfigRelativePath + ")");
            }
            catch (Exception ex)
            {
                Debug.LogError("[Unity MCP] Failed to start bridge at " + listenerPrefix + ": " + ex.Message);
                StopBridge(logWhenAlreadyStopped: false);
            }
        }

        private static bool IsPortInUse(int port)
        {
            try
            {
                var tcpListener = new TcpListener(IPAddress.Loopback, port);
                tcpListener.Start();
                tcpListener.Stop();
                return false;
            }
            catch (SocketException)
            {
                return true;
            }
        }

        private static bool IsBridgeAlive(string listenerPrefix, out string probeDetail)
        {
            try
            {
                var request = WebRequest.CreateHttp(new Uri(new Uri(listenerPrefix), "health"));
                request.Method = "GET";
                request.Timeout = HealthCheckTimeoutMs;
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    probeDetail = "health returned " + (int)response.StatusCode;
                    return response.StatusCode == HttpStatusCode.OK;
                }
            }
            catch (WebException ex) when (ex.Response is HttpWebResponse httpResponse)
            {
                probeDetail = "health returned " + (int)httpResponse.StatusCode;
                return false;
            }
            catch
            {
                probeDetail = "health probe timed out or failed";
                return false;
            }
        }

        private static void StopBridge()
        {
            StopBridge(logWhenAlreadyStopped: true);
        }

        private static void StopBridge(bool logWhenAlreadyStopped)
        {
            var listener = _listener;
            var listenerCts = _listenerCts;

            _listener = null;
            _listenerCts = null;

            try
            {
                if (listenerCts != null && !listenerCts.IsCancellationRequested)
                {
                    listenerCts.Cancel();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Unity MCP] Failed to cancel listener token: " + ex.Message);
            }

            if (listener == null)
            {
                if (logWhenAlreadyStopped)
                {
                    Debug.Log("[Unity MCP] Bridge already stopped.");
                }

                return;
            }

            try
            {
                if (listener.IsListening)
                {
                    listener.Stop();
                }

                listener.Close();
                Debug.Log("[Unity MCP] Bridge stopped.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Unity MCP] Stop encountered a socket cleanup issue, but the bridge state was cleared: " + ex.Message);
            }
        }

        private static int ResolvePort()
        {
            try
            {
                if (!File.Exists(PortConfigPath))
                {
                    return DefaultPort;
                }

                var raw = File.ReadAllText(PortConfigPath).Trim();
                if (string.IsNullOrEmpty(raw))
                {
                    return DefaultPort;
                }

                if (int.TryParse(raw, out var port) && port > 0 && port <= 65535)
                {
                    return port;
                }

                Debug.LogWarning("[Unity MCP] Invalid port in " + PortConfigRelativePath + ": " + raw + ". Falling back to " + DefaultPort + ".");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Unity MCP] Failed to read " + PortConfigRelativePath + ": " + ex.Message + ". Falling back to " + DefaultPort + ".");
            }

            return DefaultPort;
        }

        private static string BuildListenerPrefix(int port)
        {
            return "http://127.0.0.1:" + port + "/";
        }

        private static async Task ListenLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && IsRunning)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[Unity MCP] Listener error: " + ex.Message);
                    try
                    {
                        await Task.Delay(250, cancellationToken);
                    }
                    catch (TaskCanceledException)
                    {
                        return;
                    }

                    continue;
                }

                _ = Task.Run(() => HandleRequestAsync(context), cancellationToken);
            }
        }

        private static async Task HandleRequestAsync(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;
            response.Headers["Cache-Control"] = "no-cache";

            try
            {
                var method = request.HttpMethod.ToUpperInvariant();
                var path = NormalizePath(request.Url == null ? "/" : request.Url.AbsolutePath);

                if (method == "GET" && path == "/health")
                {
                    await HandleHealthAsync(response);
                    return;
                }

                if (method == "GET" && path == "/scene/current")
                {
                    await HandleCurrentSceneAsync(response);
                    return;
                }

                if (method == "POST" && path == "/play/start")
                {
                    await HandlePlayStartAsync(response);
                    return;
                }

                if (method == "POST" && path == "/play/stop")
                {
                    await HandlePlayStopAsync(response);
                    return;
                }

                if (method == "GET" && path == "/console/errors")
                {
                    await HandleConsoleErrorsAsync(request, response);
                    return;
                }

                // --- Scene manipulation endpoints ---

                if (method == "GET" && path == "/scene/hierarchy")
                {
                    await HandleSceneHierarchyAsync(request, response);
                    return;
                }

                if (method == "POST" && path == "/gameobject/find")
                {
                    await HandleGameObjectFindAsync(request, response);
                    return;
                }

                if (method == "POST" && path == "/gameobject/create")
                {
                    await HandleGameObjectCreateAsync(request, response);
                    return;
                }

                if (method == "POST" && path == "/gameobject/destroy")
                {
                    await HandleGameObjectDestroyAsync(request, response);
                    return;
                }

                if (method == "POST" && path == "/gameobject/set-active")
                {
                    await HandleGameObjectSetActiveAsync(request, response);
                    return;
                }

                if (method == "POST" && path == "/component/add")
                {
                    await HandleComponentAddAsync(request, response);
                    return;
                }

                if (method == "POST" && path == "/component/set")
                {
                    await HandleComponentSetAsync(request, response);
                    return;
                }

                if (method == "POST" && path == "/component/get")
                {
                    await HandleComponentGetAsync(request, response);
                    return;
                }

                if (method == "POST" && path == "/scene/save")
                {
                    await HandleSceneSaveAsync(response);
                    return;
                }

                await WriteJsonAsync(
                    response,
                    404,
                    new ErrorResponse
                    {
                        error = "Not found",
                        detail = method + " " + path
                    });
            }
            catch (Exception ex)
            {
                await WriteJsonAsync(
                    response,
                    500,
                    new ErrorResponse
                    {
                        error = "Bridge failure",
                        detail = ex.Message
                    });
            }
            finally
            {
                CloseResponse(response);
            }
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return "/";
            }

            var normalized = path.Trim();
            if (!normalized.StartsWith("/", StringComparison.Ordinal))
            {
                normalized = "/" + normalized;
            }

            if (normalized.Length > 1 && normalized.EndsWith("/", StringComparison.Ordinal))
            {
                normalized = normalized.Substring(0, normalized.Length - 1);
            }

            return normalized;
        }

        private static async Task HandleHealthAsync(HttpListenerResponse response)
        {
            var sceneInfo = await RunOnMainThreadAsync(() =>
            {
                var scene = SceneManager.GetActiveScene();
                return new HealthResponse
                {
                    ok = true,
                    bridgeRunning = IsRunning,
                    isPlaying = EditorApplication.isPlaying,
                    isPlayingOrWillChange = EditorApplication.isPlayingOrWillChangePlaymode,
                    isCompiling = EditorApplication.isCompiling,
                    activeScene = scene.name,
                    activeScenePath = scene.path
                };
            });

            await WriteJsonAsync(response, 200, sceneInfo);
        }

        private static async Task HandleCurrentSceneAsync(HttpListenerResponse response)
        {
            var sceneInfo = await RunOnMainThreadAsync(() =>
            {
                var scene = SceneManager.GetActiveScene();
                return new SceneResponse
                {
                    name = scene.name,
                    path = scene.path,
                    buildIndex = scene.buildIndex,
                    isLoaded = scene.isLoaded,
                    isDirty = scene.isDirty,
                    isPlaying = EditorApplication.isPlaying,
                    isPlayingOrWillChange = EditorApplication.isPlayingOrWillChangePlaymode
                };
            });

            await WriteJsonAsync(response, 200, sceneInfo);
        }

        private static async Task HandlePlayStartAsync(HttpListenerResponse response)
        {
            var result = await RunOnMainThreadAsync(() =>
            {
                if (!EditorApplication.isPlaying)
                {
                    EditorApplication.isPlaying = true;
                }

                return new PlayResponse
                {
                    action = "start",
                    isPlaying = EditorApplication.isPlaying,
                    isPlayingOrWillChange = EditorApplication.isPlayingOrWillChangePlaymode
                };
            });

            await WriteJsonAsync(response, 200, result);
        }

        private static async Task HandlePlayStopAsync(HttpListenerResponse response)
        {
            var result = await RunOnMainThreadAsync(() =>
            {
                if (EditorApplication.isPlaying)
                {
                    EditorApplication.isPlaying = false;
                }

                return new PlayResponse
                {
                    action = "stop",
                    isPlaying = EditorApplication.isPlaying,
                    isPlayingOrWillChange = EditorApplication.isPlayingOrWillChangePlaymode
                };
            });

            await WriteJsonAsync(response, 200, result);
        }

        private static async Task HandleConsoleErrorsAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            var limit = 20;
            var limitRaw = request.QueryString["limit"];
            if (!string.IsNullOrEmpty(limitRaw) && int.TryParse(limitRaw, out var parsed))
            {
                limit = Mathf.Clamp(parsed, 1, 100);
            }

            var payload = await RunOnMainThreadAsync(() =>
            {
                ErrorLogEntry[] latest;
                lock (LogLock)
                {
                    var take = Math.Min(limit, ErrorLogs.Count);
                    latest = ErrorLogs.GetRange(Math.Max(0, ErrorLogs.Count - take), take).ToArray();
                }

                return new ErrorLogsResponse
                {
                    count = latest.Length,
                    items = latest
                };
            });

            await WriteJsonAsync(response, 200, payload);
        }

        // =====================================================================
        // Scene Manipulation Handlers
        // =====================================================================

        private static async Task<string> ReadRequestBodyAsync(HttpListenerRequest request)
        {
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                return await reader.ReadToEndAsync();
            }
        }

        private static async Task HandleSceneHierarchyAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            var maxDepthRaw = request.QueryString["depth"];
            var maxDepth = 10;
            if (!string.IsNullOrEmpty(maxDepthRaw) && int.TryParse(maxDepthRaw, out var d))
                maxDepth = Mathf.Clamp(d, 1, 50);

            var pathFilter = request.QueryString["path"];

            var result = await RunOnMainThreadAsync(() =>
            {
                var scene = SceneManager.GetActiveScene();
                var roots = scene.GetRootGameObjects();

                if (!string.IsNullOrEmpty(pathFilter))
                {
                    var target = GameObject.Find(pathFilter);
                    if (target == null)
                        return new HierarchyResponse { sceneName = scene.name, nodes = new HierarchyNode[0] };

                    return new HierarchyResponse
                    {
                        sceneName = scene.name,
                        nodes = new[] { BuildHierarchyNode(target.transform, maxDepth, 0) }
                    };
                }

                var nodes = new List<HierarchyNode>();
                foreach (var root in roots)
                    nodes.Add(BuildHierarchyNode(root.transform, maxDepth, 0));

                return new HierarchyResponse { sceneName = scene.name, nodes = nodes.ToArray() };
            });

            await WriteJsonAsync(response, 200, result);
        }

        private static HierarchyNode BuildHierarchyNode(Transform t, int maxDepth, int currentDepth)
        {
            var node = new HierarchyNode
            {
                name = t.name,
                path = GetTransformPath(t),
                activeSelf = t.gameObject.activeSelf,
                components = t.GetComponents<Component>()
                    .Where(c => c != null)
                    .Select(c => c.GetType().Name)
                    .ToArray(),
                childCount = t.childCount
            };

            if (currentDepth < maxDepth)
            {
                var children = new List<HierarchyNode>();
                for (int i = 0; i < t.childCount; i++)
                    children.Add(BuildHierarchyNode(t.GetChild(i), maxDepth, currentDepth + 1));
                node.children = children.ToArray();
            }

            return node;
        }

        private static string GetTransformPath(Transform t)
        {
            var parts = new List<string>();
            var current = t;
            while (current != null)
            {
                parts.Insert(0, current.name);
                current = current.parent;
            }
            return "/" + string.Join("/", parts);
        }

        private static async Task HandleGameObjectFindAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            var body = await ReadRequestBodyAsync(request);
            var req = JsonUtility.FromJson<FindRequest>(body);

            var result = await RunOnMainThreadAsync(() =>
            {
                GameObject go = null;

                if (!string.IsNullOrEmpty(req.path))
                    go = GameObject.Find(req.path);
                else if (!string.IsNullOrEmpty(req.name))
                {
                    var all = Resources.FindObjectsOfTypeAll<GameObject>();
                    go = all.FirstOrDefault(g =>
                        g.name == req.name
                        && g.scene.isLoaded
                        && !EditorUtility.IsPersistent(g));
                }

                if (go == null)
                    return new GameObjectResponse { found = false };

                return BuildGameObjectResponse(go);
            });

            await WriteJsonAsync(response, 200, result);
        }

        private static GameObjectResponse BuildGameObjectResponse(GameObject go)
        {
            var components = go.GetComponents<Component>();
            var compInfos = new List<ComponentInfo>();
            foreach (var comp in components)
            {
                if (comp == null) continue;
                var info = new ComponentInfo
                {
                    typeName = comp.GetType().Name,
                    fullTypeName = comp.GetType().FullName
                };

                // Extract serialized properties
                var so = new SerializedObject(comp);
                var props = new List<PropertyInfo>();
                var sp = so.GetIterator();
                if (sp.NextVisible(true))
                {
                    do
                    {
                        if (sp.name == "m_Script") continue;
                        props.Add(new PropertyInfo
                        {
                            name = sp.name,
                            type = sp.propertyType.ToString(),
                            value = GetSerializedPropertyValue(sp)
                        });
                    } while (sp.NextVisible(false));
                }
                info.properties = props.ToArray();
                compInfos.Add(info);
            }

            return new GameObjectResponse
            {
                found = true,
                name = go.name,
                path = GetTransformPath(go.transform),
                activeSelf = go.activeSelf,
                layer = LayerMask.LayerToName(go.layer),
                tag = go.tag,
                components = compInfos.ToArray()
            };
        }

        private static string GetSerializedPropertyValue(SerializedProperty sp)
        {
            switch (sp.propertyType)
            {
                case SerializedPropertyType.Integer: return sp.intValue.ToString();
                case SerializedPropertyType.Boolean: return sp.boolValue.ToString();
                case SerializedPropertyType.Float: return sp.floatValue.ToString("F4");
                case SerializedPropertyType.String: return sp.stringValue ?? "";
                case SerializedPropertyType.Enum: return sp.enumDisplayNames != null && sp.enumValueIndex >= 0 && sp.enumValueIndex < sp.enumDisplayNames.Length
                    ? sp.enumDisplayNames[sp.enumValueIndex] : sp.enumValueIndex.ToString();
                case SerializedPropertyType.ObjectReference:
                    return FormatObjectReferenceValue(sp.objectReferenceValue);
                case SerializedPropertyType.Color:
                    var c = sp.colorValue;
                    return string.Format("({0:F2},{1:F2},{2:F2},{3:F2})", c.r, c.g, c.b, c.a);
                case SerializedPropertyType.Vector2:
                    var v2 = sp.vector2Value;
                    return string.Format("({0:F2},{1:F2})", v2.x, v2.y);
                case SerializedPropertyType.Vector3:
                    var v3 = sp.vector3Value;
                    return string.Format("({0:F2},{1:F2},{2:F2})", v3.x, v3.y, v3.z);
                case SerializedPropertyType.Vector4:
                    var v4 = sp.vector4Value;
                    return string.Format("({0:F2},{1:F2},{2:F2},{3:F2})", v4.x, v4.y, v4.z, v4.w);
                case SerializedPropertyType.Rect:
                    var r = sp.rectValue;
                    return string.Format("(x:{0:F1},y:{1:F1},w:{2:F1},h:{3:F1})", r.x, r.y, r.width, r.height);
                default: return "(" + sp.propertyType + ")";
            }
        }

        private static async Task HandleGameObjectCreateAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            var body = await ReadRequestBodyAsync(request);
            var req = JsonUtility.FromJson<CreateRequest>(body);

            var result = await RunOnMainThreadAsync(() =>
            {
                Transform parent = null;
                if (!string.IsNullOrEmpty(req.parent))
                {
                    var parentGo = GameObject.Find(req.parent);
                    if (parentGo == null)
                        throw new Exception("Parent not found: " + req.parent);
                    parent = parentGo.transform;
                }

                var go = new GameObject(string.IsNullOrEmpty(req.name) ? "New GameObject" : req.name);
                if (parent != null)
                    go.transform.SetParent(parent, false);

                Undo.RegisterCreatedObjectUndo(go, "MCP Create " + go.name);

                // Add components
                if (req.components != null)
                {
                    foreach (var compName in req.components)
                    {
                        AddComponentByName(go, compName);
                    }
                }

                EditorSceneManager.MarkSceneDirty(go.scene);

                return new CreateResponse
                {
                    name = go.name,
                    path = GetTransformPath(go.transform),
                    instanceId = go.GetInstanceID()
                };
            });

            await WriteJsonAsync(response, 200, result);
        }

        private static Component AddComponentByName(GameObject go, string typeName)
        {
            // Try common Unity UI types first
            var knownTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
            {
                { "RectTransform", typeof(RectTransform) },
                { "Canvas", typeof(Canvas) },
                { "CanvasScaler", typeof(UnityEngine.UI.CanvasScaler) },
                { "GraphicRaycaster", typeof(UnityEngine.UI.GraphicRaycaster) },
                { "Image", typeof(UnityEngine.UI.Image) },
                { "RawImage", typeof(UnityEngine.UI.RawImage) },
                { "Button", typeof(UnityEngine.UI.Button) },
                { "Toggle", typeof(UnityEngine.UI.Toggle) },
                { "Slider", typeof(UnityEngine.UI.Slider) },
                { "Scrollbar", typeof(UnityEngine.UI.Scrollbar) },
                { "ScrollRect", typeof(UnityEngine.UI.ScrollRect) },
                { "InputField", typeof(UnityEngine.UI.InputField) },
                { "Text", typeof(UnityEngine.UI.Text) },
                { "Dropdown", typeof(UnityEngine.UI.Dropdown) },
                { "Mask", typeof(UnityEngine.UI.Mask) },
                { "RectMask2D", typeof(UnityEngine.UI.RectMask2D) },
                { "LayoutElement", typeof(UnityEngine.UI.LayoutElement) },
                { "ContentSizeFitter", typeof(UnityEngine.UI.ContentSizeFitter) },
                { "AspectRatioFitter", typeof(UnityEngine.UI.AspectRatioFitter) },
                { "HorizontalLayoutGroup", typeof(UnityEngine.UI.HorizontalLayoutGroup) },
                { "VerticalLayoutGroup", typeof(UnityEngine.UI.VerticalLayoutGroup) },
                { "GridLayoutGroup", typeof(UnityEngine.UI.GridLayoutGroup) },
                { "CanvasGroup", typeof(CanvasGroup) },
            };

            if (knownTypes.TryGetValue(typeName, out var knownType))
            {
                return Undo.AddComponent(go, knownType);
            }

            // Try TMP types by reflection (avoid hard dependency)
            if (typeName.Equals("TextMeshProUGUI", StringComparison.OrdinalIgnoreCase)
                || typeName.Equals("TMP_Text", StringComparison.OrdinalIgnoreCase))
            {
                var tmpType = FindTypeByName("TMPro.TextMeshProUGUI");
                if (tmpType != null)
                    return Undo.AddComponent(go, tmpType);
                throw new Exception("TextMeshPro not found. Install TMP package first.");
            }

            if (typeName.Equals("TMP_InputField", StringComparison.OrdinalIgnoreCase))
            {
                var tmpType = FindTypeByName("TMPro.TMP_InputField");
                if (tmpType != null)
                    return Undo.AddComponent(go, tmpType);
                throw new Exception("TextMeshPro not found. Install TMP package first.");
            }

            if (typeName.Equals("TMP_Dropdown", StringComparison.OrdinalIgnoreCase))
            {
                var tmpType = FindTypeByName("TMPro.TMP_Dropdown");
                if (tmpType != null)
                    return Undo.AddComponent(go, tmpType);
                throw new Exception("TextMeshPro not found. Install TMP package first.");
            }

            // Generic fallback: search all loaded assemblies
            var foundType = FindTypeByName(typeName);
            if (foundType != null && typeof(Component).IsAssignableFrom(foundType))
                return Undo.AddComponent(go, foundType);

            throw new Exception("Component type not found: " + typeName);
        }

        private static Type FindTypeByName(string fullOrShortName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = asm.GetType(fullOrShortName, false, true);
                    if (type != null) return type;
                }
                catch { /* skip problematic assemblies */ }
            }

            // Try short name match
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = asm.GetTypes().FirstOrDefault(t =>
                        t.Name.Equals(fullOrShortName, StringComparison.OrdinalIgnoreCase));
                    if (type != null) return type;
                }
                catch { /* skip */ }
            }

            return null;
        }

        private static async Task HandleGameObjectDestroyAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            var body = await ReadRequestBodyAsync(request);
            var req = JsonUtility.FromJson<FindRequest>(body);

            var result = await RunOnMainThreadAsync(() =>
            {
                var path = !string.IsNullOrEmpty(req.path) ? req.path : req.name;
                var go = GameObject.Find(path);
                if (go == null)
                    throw new Exception("GameObject not found: " + path);

                Undo.DestroyObjectImmediate(go);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

                return new GenericResponse { success = true, message = "Destroyed: " + path };
            });

            await WriteJsonAsync(response, 200, result);
        }

        private static async Task HandleGameObjectSetActiveAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            var body = await ReadRequestBodyAsync(request);
            var req = JsonUtility.FromJson<GameObjectSetActiveRequest>(body);

            var result = await RunOnMainThreadAsync(() =>
            {
                var go = GameObject.Find(req.path);
                if (go == null)
                {
                    throw new Exception("GameObject not found: " + req.path);
                }

                Undo.RecordObject(go, "MCP SetActive " + req.path);
                go.SetActive(req.active);
                EditorSceneManager.MarkSceneDirty(go.scene);

                return new GenericResponse
                {
                    success = true,
                    message = "SetActive(" + req.active + "): " + req.path
                };
            });

            await WriteJsonAsync(response, 200, result);
        }

        private static async Task HandleComponentAddAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            var body = await ReadRequestBodyAsync(request);
            var req = JsonUtility.FromJson<ComponentAddRequest>(body);

            var result = await RunOnMainThreadAsync(() =>
            {
                var go = GameObject.Find(req.gameObjectPath);
                if (go == null)
                    throw new Exception("GameObject not found: " + req.gameObjectPath);

                var comp = AddComponentByName(go, req.componentType);
                EditorSceneManager.MarkSceneDirty(go.scene);

                return new GenericResponse
                {
                    success = true,
                    message = "Added " + comp.GetType().Name + " to " + req.gameObjectPath
                };
            });

            await WriteJsonAsync(response, 200, result);
        }

        private static async Task HandleComponentSetAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            var body = await ReadRequestBodyAsync(request);
            var req = JsonUtility.FromJson<ComponentSetRequest>(body);

            var result = await RunOnMainThreadAsync(() =>
            {
                var go = GameObject.Find(req.gameObjectPath);
                if (go == null)
                    throw new Exception("GameObject not found: " + req.gameObjectPath);

                // Find the component
                Component comp = null;
                if (!string.IsNullOrEmpty(req.componentType))
                {
                    var components = go.GetComponents<Component>();
                    comp = components.FirstOrDefault(c =>
                        c != null && (c.GetType().Name.Equals(req.componentType, StringComparison.OrdinalIgnoreCase)
                            || c.GetType().FullName.Equals(req.componentType, StringComparison.OrdinalIgnoreCase)));
                }

                if (comp == null)
                    throw new Exception("Component not found: " + req.componentType + " on " + req.gameObjectPath);

                var so = new SerializedObject(comp);
                var sp = so.FindProperty(req.propertyName);
                if (sp == null)
                    throw new Exception("Property not found: " + req.propertyName + " on " + req.componentType);

                Undo.RecordObject(comp, "MCP Set " + req.propertyName);
                SetSerializedPropertyValue(sp, req.value, req.assetPath);
                so.ApplyModifiedProperties();
                EditorSceneManager.MarkSceneDirty(go.scene);

                return new GenericResponse
                {
                    success = true,
                    message = "Set " + req.componentType + "." + req.propertyName + " = " + req.value
                };
            });

            await WriteJsonAsync(response, 200, result);
        }

        private static void SetSerializedPropertyValue(SerializedProperty sp, string value, string assetPath)
        {
            switch (sp.propertyType)
            {
                case SerializedPropertyType.Integer:
                    sp.intValue = int.Parse(value);
                    break;
                case SerializedPropertyType.Boolean:
                    sp.boolValue = bool.Parse(value);
                    break;
                case SerializedPropertyType.Float:
                    sp.floatValue = float.Parse(value);
                    break;
                case SerializedPropertyType.String:
                    sp.stringValue = value;
                    break;
                case SerializedPropertyType.Enum:
                    if (int.TryParse(value, out var enumIdx))
                        sp.enumValueIndex = enumIdx;
                    else
                    {
                        var idx = Array.IndexOf(sp.enumDisplayNames, value);
                        if (idx >= 0) sp.enumValueIndex = idx;
                        else throw new Exception("Invalid enum value: " + value + ". Options: " + string.Join(", ", sp.enumDisplayNames));
                    }
                    break;
                case SerializedPropertyType.Color:
                    if (ColorUtility.TryParseHtmlString(value, out var color))
                        sp.colorValue = color;
                    else
                        throw new Exception("Invalid color: " + value + ". Use #RRGGBB or #RRGGBBAA.");
                    break;
                case SerializedPropertyType.Vector2:
                    var v2parts = ParseFloatArray(value);
                    if (v2parts.Length >= 2) sp.vector2Value = new Vector2(v2parts[0], v2parts[1]);
                    break;
                case SerializedPropertyType.Vector3:
                    var v3parts = ParseFloatArray(value);
                    if (v3parts.Length >= 3) sp.vector3Value = new Vector3(v3parts[0], v3parts[1], v3parts[2]);
                    break;
                case SerializedPropertyType.Vector4:
                    var v4parts = ParseFloatArray(value);
                    if (v4parts.Length >= 4) sp.vector4Value = new Vector4(v4parts[0], v4parts[1], v4parts[2], v4parts[3]);
                    break;
                case SerializedPropertyType.Rect:
                    var rparts = ParseFloatArray(value);
                    if (rparts.Length >= 4) sp.rectValue = new Rect(rparts[0], rparts[1], rparts[2], rparts[3]);
                    break;
                case SerializedPropertyType.ObjectReference:
                    sp.objectReferenceValue = ResolveObjectReference(sp, value, assetPath);
                    break;
                default:
                    throw new Exception("Unsupported property type: " + sp.propertyType);
            }
        }

        private static string FormatObjectReferenceValue(UnityEngine.Object reference)
        {
            if (reference == null)
            {
                return "(null)";
            }

            if (reference is GameObject go)
            {
                return GetTransformPath(go.transform);
            }

            if (reference is Component component)
            {
                return GetTransformPath(component.transform) + "::" + component.GetType().Name;
            }

            var assetPath = AssetDatabase.GetAssetPath(reference);
            if (!string.IsNullOrEmpty(assetPath))
            {
                return assetPath;
            }

            return reference.name;
        }

        private static UnityEngine.Object ResolveObjectReference(SerializedProperty sp, string value, string assetPath)
        {
            var reference = !string.IsNullOrEmpty(assetPath) ? assetPath : value;
            if (string.IsNullOrEmpty(reference) || reference == "(null)")
            {
                return null;
            }

            if (reference.StartsWith("/", StringComparison.Ordinal))
            {
                return ResolveSceneObjectReference(sp, reference);
            }

            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(reference);
            if (asset == null)
            {
                throw new Exception("Asset not found at path: " + reference);
            }

            return asset;
        }

        private static UnityEngine.Object ResolveSceneObjectReference(SerializedProperty sp, string reference)
        {
            var separatorIndex = reference.IndexOf("::", StringComparison.Ordinal);
            var gameObjectPath = separatorIndex >= 0 ? reference.Substring(0, separatorIndex) : reference;
            var componentTypeName = separatorIndex >= 0 ? reference.Substring(separatorIndex + 2) : null;

            var targetGo = GameObject.Find(gameObjectPath);
            if (targetGo == null)
            {
                throw new Exception("Scene object not found: " + gameObjectPath);
            }

            if (!string.IsNullOrEmpty(componentTypeName))
            {
                var explicitComponent = targetGo
                    .GetComponents<Component>()
                    .FirstOrDefault(c => c != null && MatchesTypeName(c.GetType(), componentTypeName));

                if (explicitComponent == null)
                {
                    throw new Exception("Component not found on scene object: " + reference);
                }

                return explicitComponent;
            }

            var fieldType = ResolveSerializedPropertyFieldType(sp);
            if (fieldType == null || fieldType == typeof(UnityEngine.Object))
            {
                return targetGo;
            }

            if (typeof(GameObject).IsAssignableFrom(fieldType))
            {
                return targetGo;
            }

            if (typeof(Transform).IsAssignableFrom(fieldType))
            {
                return targetGo.transform;
            }

            if (typeof(Component).IsAssignableFrom(fieldType))
            {
                var component = targetGo.GetComponent(fieldType);
                if (component == null)
                {
                    throw new Exception("Component of type " + fieldType.Name + " not found on " + gameObjectPath);
                }

                return component;
            }

            throw new Exception("Unsupported scene reference field type: " + fieldType.FullName);
        }

        private static Type ResolveSerializedPropertyFieldType(SerializedProperty sp)
        {
            var currentType = sp.serializedObject.targetObject.GetType();
            var path = sp.propertyPath.Replace(".Array.data[", "[");
            var segments = path.Split('.');

            foreach (var rawSegment in segments)
            {
                var fieldName = rawSegment;
                var indexStart = rawSegment.IndexOf('[', StringComparison.Ordinal);
                if (indexStart >= 0)
                {
                    fieldName = rawSegment.Substring(0, indexStart);
                }

                var field = FindFieldInTypeHierarchy(currentType, fieldName);
                if (field == null)
                {
                    return null;
                }

                currentType = field.FieldType;
                if (indexStart >= 0 && currentType.IsArray)
                {
                    currentType = currentType.GetElementType();
                }
            }

            return currentType;
        }

        private static FieldInfo FindFieldInTypeHierarchy(Type type, string fieldName)
        {
            while (type != null)
            {
                var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    return field;
                }

                type = type.BaseType;
            }

            return null;
        }

        private static bool MatchesTypeName(Type type, string typeName)
        {
            return type.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrEmpty(type.FullName) && type.FullName.Equals(typeName, StringComparison.OrdinalIgnoreCase));
        }

        private static float[] ParseFloatArray(string value)
        {
            var cleaned = value.Trim('(', ')', ' ');
            var parts = cleaned.Split(',');
            var result = new float[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                result[i] = float.Parse(parts[i].Trim());
            return result;
        }

        private static async Task HandleComponentGetAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            var body = await ReadRequestBodyAsync(request);
            var req = JsonUtility.FromJson<ComponentGetRequest>(body);

            var result = await RunOnMainThreadAsync(() =>
            {
                var go = GameObject.Find(req.gameObjectPath);
                if (go == null)
                    throw new Exception("GameObject not found: " + req.gameObjectPath);

                Component comp = null;
                var components = go.GetComponents<Component>();
                comp = components.FirstOrDefault(c =>
                    c != null && (c.GetType().Name.Equals(req.componentType, StringComparison.OrdinalIgnoreCase)
                        || c.GetType().FullName.Equals(req.componentType, StringComparison.OrdinalIgnoreCase)));

                if (comp == null)
                    throw new Exception("Component not found: " + req.componentType + " on " + req.gameObjectPath);

                var so = new SerializedObject(comp);
                var props = new List<PropertyInfo>();
                var sp = so.GetIterator();
                if (sp.NextVisible(true))
                {
                    do
                    {
                        if (sp.name == "m_Script") continue;
                        props.Add(new PropertyInfo
                        {
                            name = sp.name,
                            type = sp.propertyType.ToString(),
                            value = GetSerializedPropertyValue(sp)
                        });
                    } while (sp.NextVisible(false));
                }

                return new ComponentGetResponse
                {
                    gameObjectPath = req.gameObjectPath,
                    componentType = comp.GetType().Name,
                    properties = props.ToArray()
                };
            });

            await WriteJsonAsync(response, 200, result);
        }

        private static async Task HandleSceneSaveAsync(HttpListenerResponse response)
        {
            var result = await RunOnMainThreadAsync(() =>
            {
                var scene = SceneManager.GetActiveScene();
                var saved = EditorSceneManager.SaveScene(scene);
                return new GenericResponse
                {
                    success = saved,
                    message = saved ? "Scene saved: " + scene.path : "Failed to save scene"
                };
            });

            await WriteJsonAsync(response, 200, result);
        }

        // =====================================================================
        // Original Handlers
        // =====================================================================

        private static async Task WriteJsonAsync(HttpListenerResponse response, int statusCode, object payload)
        {
            response.StatusCode = statusCode;
            response.ContentType = "application/json; charset=utf-8";

            var json = JsonUtility.ToJson(payload);
            var buffer = Encoding.UTF8.GetBytes(json);
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        }

        private static void CloseResponse(HttpListenerResponse response)
        {
            try
            {
                response.OutputStream.Close();
                response.Close();
            }
            catch
            {
                // Ignore response close failures.
            }
        }

        private static Task<T> RunOnMainThreadAsync<T>(Func<T> operation)
        {
            if (Thread.CurrentThread.ManagedThreadId == _mainThreadId)
            {
                try
                {
                    return Task.FromResult(operation());
                }
                catch (Exception ex)
                {
                    return Task.FromException<T>(ex);
                }
            }

            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            MainThreadActions.Enqueue(() =>
            {
                try
                {
                    tcs.SetResult(operation());
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            return tcs.Task;
        }

        private static void DrainMainThreadActions()
        {
            while (MainThreadActions.TryDequeue(out var action))
            {
                try
                {
                    action.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[Unity MCP] Main-thread action failed: " + ex.Message);
                }
            }
        }

        private static void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
            {
                return;
            }

            var entry = new ErrorLogEntry
            {
                timestampUtc = DateTime.UtcNow.ToString("o"),
                type = type.ToString(),
                message = Truncate(condition, MaxLogMessageLength),
                stackTrace = Truncate(stackTrace, MaxStackTraceLength)
            };

            lock (LogLock)
            {
                if (ErrorLogs.Count >= MaxStoredLogs)
                {
                    ErrorLogs.RemoveAt(0);
                }

                ErrorLogs.Add(entry);
            }
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (value.Length <= maxLength)
            {
                return value;
            }

            return value.Substring(0, maxLength);
        }

        [Serializable]
        private sealed class HealthResponse
        {
            public bool ok;
            public bool bridgeRunning;
            public bool isPlaying;
            public bool isPlayingOrWillChange;
            public bool isCompiling;
            public string activeScene;
            public string activeScenePath;
        }

        [Serializable]
        private sealed class SceneResponse
        {
            public string name;
            public string path;
            public int buildIndex;
            public bool isLoaded;
            public bool isDirty;
            public bool isPlaying;
            public bool isPlayingOrWillChange;
        }

        [Serializable]
        private sealed class PlayResponse
        {
            public string action;
            public bool isPlaying;
            public bool isPlayingOrWillChange;
        }

        [Serializable]
        private sealed class ErrorResponse
        {
            public string error;
            public string detail;
        }

        [Serializable]
        private sealed class ErrorLogsResponse
        {
            public int count;
            public ErrorLogEntry[] items;
        }

        [Serializable]
        private sealed class ErrorLogEntry
        {
            public string timestampUtc;
            public string type;
            public string message;
            public string stackTrace;
        }

        // --- Scene manipulation DTOs ---

        [Serializable]
        private sealed class HierarchyResponse
        {
            public string sceneName;
            public HierarchyNode[] nodes;
        }

        [Serializable]
        private sealed class HierarchyNode
        {
            public string name;
            public string path;
            public bool activeSelf;
            public string[] components;
            public int childCount;
            public HierarchyNode[] children;
        }

        [Serializable]
        private sealed class FindRequest
        {
            public string name;
            public string path;
        }

        [Serializable]
        private sealed class GameObjectResponse
        {
            public bool found;
            public string name;
            public string path;
            public bool activeSelf;
            public string layer;
            public string tag;
            public ComponentInfo[] components;
        }

        [Serializable]
        private sealed class ComponentInfo
        {
            public string typeName;
            public string fullTypeName;
            public PropertyInfo[] properties;
        }

        [Serializable]
        private sealed class PropertyInfo
        {
            public string name;
            public string type;
            public string value;
        }

        [Serializable]
        private sealed class CreateRequest
        {
            public string name;
            public string parent;
            public string[] components;
        }

        [Serializable]
        private sealed class CreateResponse
        {
            public string name;
            public string path;
            public int instanceId;
        }

        [Serializable]
        private sealed class ComponentAddRequest
        {
            public string gameObjectPath;
            public string componentType;
        }

        [Serializable]
        private sealed class GameObjectSetActiveRequest
        {
            public string path;
            public bool active;
        }

        [Serializable]
        private sealed class ComponentSetRequest
        {
            public string gameObjectPath;
            public string componentType;
            public string propertyName;
            public string value;
            public string assetPath;
        }

        [Serializable]
        private sealed class ComponentGetRequest
        {
            public string gameObjectPath;
            public string componentType;
        }

        [Serializable]
        private sealed class ComponentGetResponse
        {
            public string gameObjectPath;
            public string componentType;
            public PropertyInfo[] properties;
        }

        [Serializable]
        private sealed class GenericResponse
        {
            public bool success;
            public string message;
        }
    }
}
#endif
