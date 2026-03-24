namespace Features.Lobby.Application.Events
{
    public readonly struct SceneLoadRequestedEvent
    {
        public string SceneName { get; }

        public SceneLoadRequestedEvent(string sceneName)
        {
            SceneName = sceneName;
        }
    }
}
