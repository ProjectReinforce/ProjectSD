using Shared.Math;

namespace Features.Player.Domain
{
    public static class MovementRule
    {
        public static Float3 CalculateDelta(Float2 input, float speed, float deltaTime)
        {
            var normalized = input.Normalized;
            return new Float3(normalized.X * speed * deltaTime, 0f, normalized.Y * speed * deltaTime);
        }

        public static float SelectSpeed(float baseSpeed, bool isSprinting, float sprintMultiplier)
        {
            return isSprinting ? baseSpeed * sprintMultiplier : baseSpeed;
        }

        public static float ApplyGravity(float verticalVelocity, float gravity, float deltaTime, bool isGrounded)
        {
            if (isGrounded && verticalVelocity < 0f)
                return -0.5f;

            return verticalVelocity - gravity * deltaTime;
        }
    }
}
