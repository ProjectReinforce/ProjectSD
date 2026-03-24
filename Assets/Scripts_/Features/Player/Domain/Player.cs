using Shared.Kernel;
using Shared.Math;

namespace Features.Player.Domain
{
    public sealed class Player : Entity
    {
        public Player(DomainEntityId id, PlayerSpec spec) : base(id)
        {
            Spec = spec;
            MaxHp = spec.MaxHp;
            CurrentHp = spec.MaxHp;
        }

        public PlayerSpec Spec { get; }
        public Float3 Position { get; private set; }
        public float VerticalVelocity { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsSprinting { get; private set; }

        public float MaxHp { get; }
        public float CurrentHp { get; private set; }
        public bool IsDead => CurrentHp <= 0f;
        public bool IsInvulnerable { get; private set; }

        public float TakeDamage(float damage)
        {
            if (IsDead || IsInvulnerable)
                return CurrentHp;

            if (damage < 0f)
                damage = 0f;

            CurrentHp -= damage;
            if (CurrentHp < 0f)
                CurrentHp = 0f;

            return CurrentHp;
        }

        public void Die()
        {
            CurrentHp = 0f;
        }

        public void Respawn()
        {
            CurrentHp = MaxHp;
            IsInvulnerable = false;
        }

        public void SetInvulnerable(bool value)
        {
            IsInvulnerable = value;
        }

        public Float3 CalculateMovement(Float2 input, float deltaTime)
        {
            var speed = MovementRule.SelectSpeed(Spec.WalkSpeed, IsSprinting, Spec.SprintMultiplier);
            var horizontal = MovementRule.CalculateDelta(input, speed, deltaTime);

            VerticalVelocity = MovementRule.ApplyGravity(VerticalVelocity, Spec.Gravity, deltaTime, IsGrounded);

            return new Float3(horizontal.X, VerticalVelocity * deltaTime, horizontal.Z);
        }

        public bool TryJump()
        {
            if (!IsGrounded || IsDead)
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
