using Shared.Kernel;

namespace Features.Lobby.Application.Ports
{
    public interface ILobbyTeamNetworkPort
    {
        Result ChangeTeam(EntityId roomId, EntityId memberId, TeamType team);
        Result SetReady(EntityId roomId, EntityId memberId, bool isReady);
    }
}
