namespace Features.Player.Domain
{
    public readonly struct PlayerSpec
    {
        public float WalkSpeed { get; }
        public float SprintMultiplier { get; }
        public float JumpForce { get; }
        public float Gravity { get; }

        public PlayerSpec(float walkSpeed, float sprintMultiplier, float jumpForce, float gravity)
        {
            WalkSpeed = walkSpeed;
            SprintMultiplier = sprintMultiplier;
            JumpForce = jumpForce;
            Gravity = gravity;
        }
    }
}
