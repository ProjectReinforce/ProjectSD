using System;
using System.Collections.Generic;
using System.Linq;

namespace Shared.Kernel
{
    public abstract class ValueObject : IEquatable<ValueObject>
    {
        public bool Equals(ValueObject other)
        {
            if (other is null || GetType() != other.GetType())
            {
                return false;
            }

            return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
        }

        public override bool Equals(object obj)
        {
            return obj is ValueObject other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                foreach (var component in GetEqualityComponents())
                {
                    hash = (hash * 23) + (component == null ? 0 : component.GetHashCode());
                }

                return hash;
            }
        }

        protected abstract IEnumerable<object> GetEqualityComponents();
    }
}
