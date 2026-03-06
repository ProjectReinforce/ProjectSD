using System;
using System.Collections.Generic;

namespace Shared.Kernel
{
    public abstract class ValueObject : IEquatable<ValueObject>
    {
        public bool Equals(ValueObject other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (other == null || GetType() != other.GetType())
            {
                return false;
            }

            using (var thisValues = GetEqualityComponents().GetEnumerator())
            using (var otherValues = other.GetEqualityComponents().GetEnumerator())
            {
                while (true)
                {
                    var hasThis = thisValues.MoveNext();
                    var hasOther = otherValues.MoveNext();

                    if (!hasThis && !hasOther)
                    {
                        return true;
                    }

                    if (hasThis != hasOther)
                    {
                        return false;
                    }

                    if (!Equals(thisValues.Current, otherValues.Current))
                    {
                        return false;
                    }
                }
            }
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
