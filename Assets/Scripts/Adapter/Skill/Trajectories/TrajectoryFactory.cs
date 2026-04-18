using SwDreams.Data;
using SwDreams.Shared.Data;
using SwDreams.Domain.ValueObjects;

namespace SwDreams.Adapter.Skill.Trajectories
{
    /// <summary>
    /// TrajectoryType → ITrajectoryBehavior 생성.
    /// SkillData의 파라미터를 전달하여 초기화된 인스턴스 반환.
    /// 매 투사체 스폰마다 새 인스턴스 생성 (상태를 갖는 behavior 대응).
    ///
    /// [Phase 7 리팩토링] Step 3-7b
    /// </summary>
    public static class TrajectoryFactory
    {
        public static ITrajectoryBehavior Create(TrajectoryType type, SkillData data)
        {
            switch (type)
            {
                case TrajectoryType.Straight:
                    return new StraightTrajectory();

                case TrajectoryType.Homing:
                    return new HomingTrajectory(data.homingRotateSpeed);

                case TrajectoryType.Boomerang:
                    return new BoomerangTrajectory(
                        data.hasPullOnReturn, data.pullRadius, data.pullForce);

                case TrajectoryType.Tornado:
                    return new TornadoTrajectory(
                        data.pullRadius, data.pullForce);

                case TrajectoryType.Spiral:
                    return new SpiralTrajectory(
                        data.pullRadius, data.pullForce,
                        data.spiralExpandSpeed);

                case TrajectoryType.Zigzag:
                    return new ZigzagTrajectory(
                        data.waveAmplitude, data.waveFrequency);

                case TrajectoryType.SinWave:
                    return new SinWaveTrajectory(
                        data.waveAmplitude, data.waveFrequency);

                default:
                    return new StraightTrajectory();
            }
        }
    }
}
