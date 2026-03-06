using Features.Lobby.Application;
using Shared.Kernel;

namespace Features.Lobby.Presentation
{
    public sealed class LobbyInputHandler
    {
        private readonly LobbyPresenter _presenter;

        public LobbyInputHandler(LobbyPresenter presenter)
        {
            _presenter = presenter;
        }

        public Result HandleCreateRoom(string roomName, int capacity, string ownerDisplayName)
        {
            return _presenter.CreateRoom(roomName, capacity, ownerDisplayName);
        }

        public Result HandleJoinRoom(EntityId roomId, string memberDisplayName)
        {
            return _presenter.JoinRoom(roomId, memberDisplayName);
        }

        public Result HandleLeaveRoom(EntityId roomId, EntityId memberId)
        {
            return _presenter.LeaveRoom(roomId, memberId);
        }

        public Result HandleChangeTeam(EntityId roomId, EntityId memberId, LobbyTeam team)
        {
            return _presenter.ChangeTeam(roomId, memberId, team);
        }

        public Result HandleSetReady(EntityId roomId, EntityId memberId, bool isReady)
        {
            return _presenter.SetReady(roomId, memberId, isReady);
        }

        public Result HandleStartGame(EntityId roomId)
        {
            return _presenter.StartGame(roomId);
        }
    }
}
