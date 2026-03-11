using Shared.Math;

namespace Features.Projectile.Domain.Trajectory
{
    public sealed class LinearTrajectory : ITrajectory
    {
        public Float3 Calculate(in TrajectoryInput input)
        {
            return input.Origin + input.Direction * (input.Speed * input.Elapsed);
        }
    }
}
