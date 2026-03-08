namespace Features.Lobby.Application.Events
{
    public readonly struct LobbyErrorEvent
    {
        public LobbyErrorEvent(string message) => Message = message;
        public string Message { get; }
    }
}
