using Shared.Math;

namespace Features.Projectile.Domain
{
    public interface ITrajectory
    {
        Float3 Calculate(in TrajectoryInput input);
    }
}
