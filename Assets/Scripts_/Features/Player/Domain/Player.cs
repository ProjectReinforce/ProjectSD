using Shared.Kernel;
using Shared.Math;

namespace Features.Player.Domain
{
    public sealed class Player : Entity
    {
        public Player(DomainEntityId id, PlayerSpec spec) : base(id)
        {
            Spec = spec;
        }

        public PlayerSpec Spec { get; }
        public Float3 Position { get; private set; }
        public float VerticalVelocity { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsSprinting { get; private set; }

        public Float3 CalculateMovement(Float2 input, float deltaTime)
        {
            var speed = MovementRule.SelectSpeed(Spec.WalkSpeed, IsSprinting, Spec.SprintMultiplier);
            var horizontal = MovementRule.CalculateDelta(input, speed, deltaTime);

            VerticalVelocity = MovementRule.ApplyGravity(VerticalVelocity, Spec.Gravity, deltaTime, IsGrounded);

            return new Float3(horizontal.X, VerticalVelocity * deltaTime, horizontal.Z);
        }

        public bool TryJump()
        {
            if (!IsGrounded)
                return false;

            VerticalVelocity = Spec.JumpForce;
            return true;
        }

        public void SetSprinting(bool value)
        {
            IsSprinting = value;
        }

        public void ApplyMovement(Float3 position, bool isGrounded)
        {
            Position = position;
            IsGrounded = isGrounded;
            if (isGrounded) VerticalVelocity = 0f;
        }
    }
}
