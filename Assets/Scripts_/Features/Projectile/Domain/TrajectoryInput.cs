using Shared.Math;

namespace Features.Projectile.Domain
{
    public readonly struct TrajectoryInput
    {
        public readonly Float3 Origin;
        public readonly Float3 CurrentPosition;
        public readonly Float3 Direction;
        public readonly float Speed;
        public readonly float DeltaTime;
        public readonly float Elapsed;
        public readonly Float3 TargetPosition;

        public TrajectoryInput(
            Float3 origin,
            Float3 currentPosition,
            Float3 direction,
            float speed,
            float deltaTime,
            float elapsed,
            Float3 targetPosition)
        {
            Origin = origin;
            CurrentPosition = currentPosition;
            Direction = direction;
            Speed = speed;
            DeltaTime = deltaTime;
            Elapsed = elapsed;
            TargetPosition = targetPosition;
        }
    }
}
