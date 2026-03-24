namespace Features.Player.Domain
{
    public readonly struct PlayerSpec
    {
        public float WalkSpeed { get; }
        public float SprintMultiplier { get; }
        public float JumpForce { get; }
        public float Gravity { get; }
        public float MaxHp { get; }
        public float Defense { get; }

        public PlayerSpec(
            float walkSpeed,
            float sprintMultiplier,
            float jumpForce,
            float gravity,
            float maxHp,
            float defense = 0f
        )
        {
            WalkSpeed = walkSpeed;
            SprintMultiplier = sprintMultiplier;
            JumpForce = jumpForce;
            Gravity = gravity;
            MaxHp = maxHp > 0f ? maxHp : 100f;
            Defense = defense < 0f ? 0f : defense;
        }
    }
}
