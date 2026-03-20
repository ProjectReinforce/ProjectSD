using System;

namespace Shared.Kernel
{
    public readonly struct DomainEntityId : IEquatable<DomainEntityId>
    {
        public DomainEntityId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("DomainEntityId cannot be empty.", nameof(value));
            }

            Value = value.Trim();
        }

        public string Value { get; }

        public static DomainEntityId New()
        {
            return new DomainEntityId(Guid.NewGuid().ToString("N"));
        }

        public bool Equals(DomainEntityId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is DomainEntityId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value;
        }
    }
}
