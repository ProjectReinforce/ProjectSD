using Features.Projectile.Domain.Hit;
using Features.Projectile.Domain.Trajectory;
using Shared.Kernel;

namespace Features.Projectile.Domain
{
    public sealed class ProjectileSpec : ValueObject
    {
        private const int Precision = 4;

        public ProjectileSpec(TrajectoryType trajectoryType, HitType hitType, float speed, float radius)
        {
            TrajectoryType = trajectoryType;
            HitType = hitType;
            Speed = (float)System.Math.Round(speed, Precision);
            Radius = (float)System.Math.Round(radius, Precision);
        }

        public TrajectoryType TrajectoryType { get; }
        public HitType HitType { get; }
        public float Speed { get; }
        public float Radius { get; }

        public override bool Equals(object obj)
        {
            if (obj is not ProjectileSpec other) return false;
            return TrajectoryType == other.TrajectoryType
                && HitType == other.HitType
                && Speed == other.Speed
                && Radius == other.Radius;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + TrajectoryType.GetHashCode();
                hash = hash * 31 + HitType.GetHashCode();
                hash = hash * 31 + Speed.GetHashCode();
                hash = hash * 31 + Radius.GetHashCode();
                return hash;
            }
        }
    }
}
