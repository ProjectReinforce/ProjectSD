using System;
using Shared.Math;

namespace Features.Projectile.Domain.Trajectory
{
    public enum TrajectoryType
    {
        Linear = 0,
        Parabolic = 1,
        Homing = 2,
        Orbit = 3,
        Boomerang = 4
    }

    public interface ITrajectory
    {
        Float3 Calculate(in TrajectoryInput input);
    }

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
