using Shared.Kernel;

namespace Features.Lobby.Domain
{
    public enum TeamType
    {
        None = 0,
        Red = 1,
        Blue = 2
    }

    public sealed class RoomMember : Entity
    {
        public RoomMember(EntityId id, string displayName, TeamType team, bool isReady)
            : base(id)
        {
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Player" : displayName.Trim();
            Team = team;
            IsReady = isReady;
        }

        public string DisplayName { get; }
        public TeamType Team { get; private set; }
        public bool IsReady { get; private set; }

        public Result ChangeTeam(TeamType newTeam)
        {
            Team = newTeam;
            return Result.Success();
        }

        public Result SetReady(bool isReady)
        {
            IsReady = isReady;
            return Result.Success();
        }
    }
}
