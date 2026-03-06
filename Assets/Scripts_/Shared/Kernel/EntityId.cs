using System;

namespace Shared.Kernel
{
    public readonly struct EntityId : IEquatable<EntityId>
    {
        public EntityId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("EntityId cannot be empty.", nameof(value));
            }

            Value = value.Trim();
        }

        public string Value { get; }

        public static EntityId New()
        {
            return new EntityId(Guid.NewGuid().ToString("N"));
        }

        public bool Equals(EntityId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is EntityId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value == null ? 0 : Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }
    }
}
