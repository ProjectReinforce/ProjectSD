using Shared.Math;

namespace Features.Projectile.Domain
{
    /// <summary>
    /// 궤적 계산에 필요한 입력 데이터.
    /// 전략별 사용 필드:
    ///   Linear / Parabolic / Boomerang : Origin, Direction, Speed, Elapsed
    ///   Homing                         : CurrentPosition, TargetPosition, Direction, Speed, DeltaTime
    ///   Orbit                          : TargetPosition, Speed, Elapsed
    /// </summary>
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
