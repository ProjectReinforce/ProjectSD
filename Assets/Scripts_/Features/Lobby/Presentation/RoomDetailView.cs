using Features.Lobby.Application;
using Features.Lobby.Application.Events;
using Features.Lobby.Domain;
using Shared.Kernel;
using UnityEngine;

namespace Features.Lobby.Presentation
{
    public sealed class RoomDetailView : MonoBehaviour
    {
        private LeaveRoomUseCase _leaveRoom;
        private ChangeTeamUseCase _changeTeam;
        private SetReadyUseCase _setReady;
        private StartGameUseCase _startGame;

        public void Initialize(
            LeaveRoomUseCase leaveRoom,
            ChangeTeamUseCase changeTeam,
            SetReadyUseCase setReady,
            StartGameUseCase startGame)
        {
            _leaveRoom = leaveRoom;
            _changeTeam = changeTeam;
            _setReady = setReady;
            _startGame = startGame;
        }

        public Result LeaveRoom(Shared.Kernel.EntityId roomId, Shared.Kernel.EntityId memberId)
            => _leaveRoom.Execute(roomId, memberId);

        public Result ChangeTeam(Shared.Kernel.EntityId roomId, Shared.Kernel.EntityId memberId, TeamType team)
            => _changeTeam.Execute(roomId, memberId, team);

        public Result SetReady(Shared.Kernel.EntityId roomId, Shared.Kernel.EntityId memberId, bool isReady)
            => _setReady.Execute(roomId, memberId, isReady);

        public Result StartGame(Shared.Kernel.EntityId roomId)
            => _startGame.Execute(roomId);

        public void Render(RoomSnapshot room)
        {
            Debug.Log($"[Lobby] Room detail updated: {room.Name} ({room.Members.Count}/{room.Capacity})");
        }
    }
}
