using Shared.Math;

namespace Features.Player.Application.Ports
{
    public readonly struct MotorResult
    {
        public Float3 Position { get; }
        public bool IsGrounded { get; }

        public MotorResult(Float3 position, bool isGrounded)
        {
            Position = position;
            IsGrounded = isGrounded;
        }
    }
}
