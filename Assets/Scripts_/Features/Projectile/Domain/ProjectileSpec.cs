using System.Collections.Generic;
using Shared.Kernel;

namespace Features.Projectile.Domain
{
    public sealed class ProjectileSpec : ValueObject
    {
        public ProjectileSpec(TrajectoryType trajectoryType, HitType hitType, float speed, float radius)
        {
            TrajectoryType = trajectoryType;
            HitType = hitType;
            Speed = speed;
            Radius = radius;
        }

        public TrajectoryType TrajectoryType { get; }
        public HitType HitType { get; }
        public float Speed { get; }
        public float Radius { get; }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return TrajectoryType;
            yield return HitType;
            yield return Speed;
            yield return Radius;
        }
    }
}
