using Features.Lobby.Application.Ports;
using UnityEngine.SceneManagement;
using UnityEngine;

namespace Features.Lobby.Infrastructure
{
    public sealed class SceneLoaderAdapter : ISceneLoaderPort
    {
        private const string GameSceneName = "GameScene";

        public void LoadScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogWarning("[SceneLoader] Scene name is empty, loading default GameScene.");
                sceneName = GameSceneName;
            }

            Debug.Log($"[SceneLoader] Loading scene: {sceneName}");
            SceneManager.LoadScene(sceneName);
        }
    }
}
