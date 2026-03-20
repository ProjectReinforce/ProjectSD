using Shared.Kernel;

namespace Features.Lobby.Domain
{
    public sealed class RoomMember : Entity
    {
        public RoomMember(DomainEntityId id, string displayName, TeamType team, bool isReady)
            : base(id)
        {
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Player" : displayName.Trim();
            Team = team;
            IsReady = isReady;
        }

        public string DisplayName { get; }
        public TeamType Team { get; private set; }
        public bool IsReady { get; private set; }

        public void ChangeTeam(TeamType newTeam)
        {
            Team = newTeam;
        }

        public void SetReady(bool isReady)
        {
            IsReady = isReady;
        }
    }
}
