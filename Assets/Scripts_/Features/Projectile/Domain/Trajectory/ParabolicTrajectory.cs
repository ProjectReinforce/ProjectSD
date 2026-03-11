using Shared.Math;

namespace Features.Projectile.Domain.Trajectory
{
    public sealed class ParabolicTrajectory : ITrajectory
    {
        private const float Gravity = 9.81f;

        public Float3 Calculate(in TrajectoryInput input)
        {
            var horizontal = input.Origin + input.Direction * (input.Speed * input.Elapsed);
            var verticalDrop = 0.5f * Gravity * input.Elapsed * input.Elapsed;
            return new Float3(horizontal.X, horizontal.Y - verticalDrop, horizontal.Z);
        }
    }
}
