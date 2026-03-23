using UnityEngine;

namespace SwDreams.Adapter.Skill.Trajectories
{
    /// <summary>직선 이동. 기본 궤적.</summary>
    public class StraightTrajectory : ITrajectoryBehavior
    {
        public bool Penetrates => false;
        public bool OverridesLifetime => false;

        public void Initialize(Projectile projectile) { }
        public void Reset() { }

        public void UpdateMovement(Projectile projectile, float deltaTime)
        {
            projectile.transform.position += (Vector3)(projectile.Direction * projectile.Speed * deltaTime);
        }
    }

    /// <summary>지그재그. 직선 + 좌우 진동.</summary>
    public class ZigzagTrajectory : ITrajectoryBehavior
    {
        private float amplitude;
        private float frequency;
        private float time;
        private Vector2 perpendicular;

        public bool Penetrates => false;
        public bool OverridesLifetime => false;

        public ZigzagTrajectory(float amplitude = 0.8f, float frequency = 5f)
        {
            this.amplitude = amplitude;
            this.frequency = frequency;
        }

        public void Initialize(Projectile projectile)
        {
            time = 0f;
            var dir = projectile.Direction;
            perpendicular = new Vector2(-dir.y, dir.x);
        }

        public void Reset() { time = 0f; }

        public void UpdateMovement(Projectile projectile, float deltaTime)
        {
            time += deltaTime;
            // 삼각파: Mathf.PingPong으로 날카로운 지그재그
            float wave = (Mathf.PingPong(time * frequency, 2f) - 1f) * amplitude;
            Vector2 forward = projectile.Direction * projectile.Speed * deltaTime;
            Vector2 lateral = perpendicular * wave * deltaTime;
            projectile.transform.position += (Vector3)(forward + lateral);
        }
    }

    /// <summary>사인파. 직선 + 부드러운 곡선.</summary>
    public class SinWaveTrajectory : ITrajectoryBehavior
    {
        private float amplitude;
        private float frequency;
        private float time;
        private Vector2 perpendicular;

        public bool Penetrates => false;
        public bool OverridesLifetime => false;

        public SinWaveTrajectory(float amplitude = 1f, float frequency = 3f)
        {
            this.amplitude = amplitude;
            this.frequency = frequency;
        }

        public void Initialize(Projectile projectile)
        {
            time = 0f;
            var dir = projectile.Direction;
            perpendicular = new Vector2(-dir.y, dir.x);
        }

        public void Reset() { time = 0f; }

        public void UpdateMovement(Projectile projectile, float deltaTime)
        {
            time += deltaTime;
            float wave = Mathf.Sin(time * frequency * Mathf.PI * 2f) * amplitude;
            Vector2 forward = projectile.Direction * projectile.Speed * deltaTime;
            Vector2 lateral = perpendicular * wave * deltaTime;
            projectile.transform.position += (Vector3)(forward + lateral);
        }
    }
}
