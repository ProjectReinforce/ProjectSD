using Features.Lobby.Application;
using Shared.Kernel;

namespace Features.Lobby.Presentation
{
    public sealed class LobbyInputHandler
    {
        private readonly CreateRoomUseCase _createRoom;
        private readonly JoinRoomUseCase _joinRoom;
        private readonly LeaveRoomUseCase _leaveRoom;
        private readonly ChangeTeamUseCase _changeTeam;
        private readonly SetReadyUseCase _setReady;
        private readonly StartGameUseCase _startGame;

        public LobbyInputHandler(
            CreateRoomUseCase createRoom,
            JoinRoomUseCase joinRoom,
            LeaveRoomUseCase leaveRoom,
            ChangeTeamUseCase changeTeam,
            SetReadyUseCase setReady,
            StartGameUseCase startGame)
        {
            _createRoom = createRoom;
            _joinRoom = joinRoom;
            _leaveRoom = leaveRoom;
            _changeTeam = changeTeam;
            _setReady = setReady;
            _startGame = startGame;
        }

        public Result HandleCreateRoom(string roomName, int capacity, string ownerDisplayName)
        {
            if (string.IsNullOrWhiteSpace(roomName))
                return Result.Failure("Room name is required.");
            if (capacity < 2)
                return Result.Failure("Capacity must be at least 2.");

            return _createRoom.Execute(roomName, capacity, ownerDisplayName);
        }

        public Result HandleJoinRoom(EntityId roomId, string memberDisplayName)
        {
            if (string.IsNullOrWhiteSpace(memberDisplayName))
                return Result.Failure("Display name is required.");

            return _joinRoom.Execute(roomId, memberDisplayName);
        }

        public Result HandleLeaveRoom(EntityId roomId, EntityId memberId)
            => _leaveRoom.Execute(roomId, memberId);

        public Result HandleChangeTeam(EntityId roomId, EntityId memberId, TeamType team)
            => _changeTeam.Execute(roomId, memberId, team);

        public Result HandleSetReady(EntityId roomId, EntityId memberId, bool isReady)
            => _setReady.Execute(roomId, memberId, isReady);

        public Result HandleStartGame(EntityId roomId)
            => _startGame.Execute(roomId);
    }
}
