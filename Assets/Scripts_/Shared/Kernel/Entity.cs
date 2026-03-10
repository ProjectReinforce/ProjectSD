using System;

namespace Shared.Kernel
{
    public abstract class Entity : IEquatable<Entity>
    {
        protected Entity(EntityId id)
        {
            Id = id;
        }

        public EntityId Id { get; }

        public bool Equals(Entity other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            if (GetType() != other.GetType()) return false;
            return Id.Equals(other.Id);
        }

        public override bool Equals(object obj)
        {
            return obj is Entity other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }
}
