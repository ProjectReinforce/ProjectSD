using Shared.Math;

namespace Features.Projectile.Domain.Trajectory
{
    public sealed class BoomerangTrajectory : ITrajectory
    {
        private const float TurnbackTime = 1f;

        public Float3 Calculate(in TrajectoryInput input)
        {
            var phase = input.Elapsed < TurnbackTime ? 1f : -1f;
            return input.Origin + input.Direction * (input.Speed * input.Elapsed * phase);
        }
    }
}
