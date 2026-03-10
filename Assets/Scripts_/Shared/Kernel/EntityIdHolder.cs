using UnityEngine;

namespace Shared.Kernel
{
    public sealed class EntityIdHolder : MonoBehaviour
    {
        private EntityId _id;

        public EntityId Id => _id;
        public bool IsInitialized { get; private set; }

        public void Set(EntityId id)
        {
            _id = id;
            IsInitialized = true;
        }
    }
}
