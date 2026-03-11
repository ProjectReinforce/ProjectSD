using Features.Lobby.Application.Ports;
using Shared.Time;
using Features.Lobby.Domain;
using Shared.Kernel;

namespace Features.Lobby.Application.UseCases
{
    public sealed class JoinRoomUseCase
    {
        private readonly ILobbyNetworkPort _network;
        private readonly IClockPort _clock;

        public JoinRoomUseCase(ILobbyNetworkPort network, IClockPort clock)
        {
            _network = network;
            _clock = clock;
        }

        public Result Execute(EntityId roomId, string memberDisplayName)
        {
            var name = string.IsNullOrWhiteSpace(memberDisplayName) ? "Player" : memberDisplayName.Trim();
            var member = new RoomMember(_clock.NewId(), name, TeamType.None, false);
            return _network.JoinRoom(roomId, member);
        }
    }
}
