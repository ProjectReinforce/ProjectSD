#if UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectSD.EditorTools.UnityMcp
{
    [InitializeOnLoad]
    internal static class UnityMcpBridge
    {
        private const string ListenerPrefix = "http://127.0.0.1:51234/";
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
            Debug.LogFormat(
                "[Unity MCP] running={0} playing={1} compiling={2} scene={3}",
                IsRunning,
                EditorApplication.isPlaying,
                EditorApplication.isCompiling,
                SceneManager.GetActiveScene().path);
        }

        private static bool IsRunning
        {
            get { return _listener != null && _listener.IsListening; }
        }

        private static void StartBridge()
        {
            if (IsRunning)
            {
                return;
            }

            StopBridge();

            try
            {
                _listenerCts = new CancellationTokenSource();
                _listener = new HttpListener();
                _listener.Prefixes.Add(ListenerPrefix);
                _listener.Start();
                Task.Run(() => ListenLoopAsync(_listenerCts.Token));
                Debug.Log("[Unity MCP] Bridge started at " + ListenerPrefix);
            }
            catch (Exception ex)
            {
                Debug.LogError("[Unity MCP] Failed to start bridge: " + ex.Message);
                StopBridge();
            }
        }

        private static void StopBridge()
        {
            try
            {
                if (_listenerCts != null && !_listenerCts.IsCancellationRequested)
                {
                    _listenerCts.Cancel();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Unity MCP] Failed to cancel listener token: " + ex.Message);
            }

            try
            {
                if (_listener != null)
                {
                    _listener.Stop();
                    _listener.Close();
                    _listener = null;
                    Debug.Log("[Unity MCP] Bridge stopped.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Unity MCP] Failed to stop bridge: " + ex.Message);
            }
            finally
            {
                _listener = null;
                _listenerCts = null;
            }
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
    }
}
#endif
