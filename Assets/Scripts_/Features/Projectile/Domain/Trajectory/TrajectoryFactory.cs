using System;

namespace Features.Projectile.Domain
{
    public static class TrajectoryFactory
    {
        public static ITrajectory Create(TrajectoryType type)
        {
            switch (type)
            {
                case TrajectoryType.Linear: return new LinearTrajectory();
                case TrajectoryType.Parabolic: return new ParabolicTrajectory();
                case TrajectoryType.Homing: return new HomingTrajectory();
                case TrajectoryType.Orbit: return new OrbitTrajectory();
                case TrajectoryType.Boomerang: return new BoomerangTrajectory();
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
        }
    }
}
