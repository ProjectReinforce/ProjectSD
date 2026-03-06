namespace Shared.Kernel
{
    public abstract class Entity
    {
        protected Entity(EntityId id)
        {
            Id = id;
        }

        public EntityId Id { get; }
    }
}
