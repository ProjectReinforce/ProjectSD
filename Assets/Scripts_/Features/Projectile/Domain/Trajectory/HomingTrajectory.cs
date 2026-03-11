using Shared.Math;

namespace Features.Projectile.Domain.Trajectory
{
    public sealed class HomingTrajectory : ITrajectory
    {
        private const float TurnRate = 5f;

        public Float3 Calculate(in TrajectoryInput input)
        {
            var toTarget = (input.TargetPosition - input.CurrentPosition).Normalized;
            var blended = Float3.Lerp(input.Direction, toTarget, TurnRate * input.DeltaTime).Normalized;
            return input.CurrentPosition + blended * (input.Speed * input.DeltaTime);
        }
    }
}
