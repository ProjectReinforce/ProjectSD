using Shared.Math;

namespace Features.Projectile.Domain
{
    public sealed class OrbitTrajectory : ITrajectory
    {
        private const float OrbitRadius = 3f;

        public Float3 Calculate(in TrajectoryInput input)
        {
            var angle = input.Speed * input.Elapsed;
            var cos = (float)System.Math.Cos(angle);
            var sin = (float)System.Math.Sin(angle);
            return new Float3(
                input.TargetPosition.X + OrbitRadius * cos,
                input.TargetPosition.Y,
                input.TargetPosition.Z + OrbitRadius * sin);
        }
    }
}
