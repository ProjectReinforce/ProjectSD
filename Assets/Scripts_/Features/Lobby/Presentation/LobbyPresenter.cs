using Features.Lobby.Application;
using Features.Lobby.Application.Ports;
using Features.Lobby.Domain;
using Shared.Kernel;
using UnityEngine;
using EntityId = Shared.Kernel.EntityId;

using DomainLobby = Features.Lobby.Domain.Lobby;

namespace Features.Lobby.Presentation
{
    public sealed class LobbyPresenter : ILobbyOutputPort
    {
        private readonly LobbyView _view;
        private readonly CreateRoomUseCase _createRoomUseCase;
        private readonly JoinRoomUseCase _joinRoomUseCase;
        private readonly LeaveRoomUseCase _leaveRoomUseCase;
        private readonly ChangeTeamUseCase _changeTeamUseCase;
        private readonly SetReadyUseCase _setReadyUseCase;
        private readonly StartGameUseCase _startGameUseCase;

        public LobbyPresenter(
            LobbyView view,
            CreateRoomUseCase createRoomUseCase,
            JoinRoomUseCase joinRoomUseCase,
            LeaveRoomUseCase leaveRoomUseCase,
            ChangeTeamUseCase changeTeamUseCase,
            SetReadyUseCase setReadyUseCase,
            StartGameUseCase startGameUseCase)
        {
            _view = view;
            _createRoomUseCase = createRoomUseCase;
            _joinRoomUseCase = joinRoomUseCase;
            _leaveRoomUseCase = leaveRoomUseCase;
            _changeTeamUseCase = changeTeamUseCase;
            _setReadyUseCase = setReadyUseCase;
            _startGameUseCase = startGameUseCase;
        }

        public Result CreateRoom(string roomName, int capacity, string ownerDisplayName)
        {
            return _createRoomUseCase.Execute(roomName, capacity, ownerDisplayName, this);
        }

        public Result JoinRoom(EntityId roomId, string memberDisplayName)
        {
            return _joinRoomUseCase.Execute(roomId, memberDisplayName, this);
        }

        public Result LeaveRoom(EntityId roomId, EntityId memberId)
        {
            return _leaveRoomUseCase.Execute(roomId, memberId, this);
        }

        public Result ChangeTeam(EntityId roomId, EntityId memberId, TeamType team)
        {
            return _changeTeamUseCase.Execute(roomId, memberId, team, this);
        }

        public Result SetReady(EntityId roomId, EntityId memberId, bool isReady)
        {
            return _setReadyUseCase.Execute(roomId, memberId, isReady, this);
        }

        public Result StartGame(EntityId roomId)
        {
            return _startGameUseCase.Execute(roomId, this);
        }

        public void ShowLobby(DomainLobby lobby)
        {
            if (_view == null)
            {
                Debug.LogError("[LobbyPresenter] LobbyView is not assigned.");
                return;
            }

            _view.RenderLobby(lobby);
        }

        public void ShowRoom(Room room)
        {
            if (_view == null)
            {
                Debug.LogError("[LobbyPresenter] LobbyView is not assigned.");
                return;
            }

            _view.RenderRoom(room);
        }

        public void ShowStartGame(Room room)
        {
            if (_view == null)
            {
                Debug.LogError("[LobbyPresenter] LobbyView is not assigned.");
                return;
            }

            _view.RenderStartGame(room);
        }

        public void ShowError(string message)
        {
            if (_view == null)
            {
                Debug.LogError("[LobbyPresenter] LobbyView is not assigned.");
                return;
            }

            _view.RenderError(message);
        }
    }
}
