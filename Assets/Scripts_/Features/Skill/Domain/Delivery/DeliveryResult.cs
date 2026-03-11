namespace Features.Skill.Domain.Delivery
{
    public readonly struct DeliveryResult
    {
        public DeliveryResult(string description)
        {
            Description = description ?? string.Empty;
        }

        public string Description { get; }
    }
}
