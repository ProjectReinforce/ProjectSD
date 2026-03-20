using UnityEngine;

namespace Shared.Kernel
{
    public sealed class EntityIdHolder : MonoBehaviour
    {
        private DomainEntityId _id;

        public DomainEntityId Id => _id;
        public bool IsInitialized { get; private set; }

        public void Set(DomainEntityId id)
        {
            _id = id;
            IsInitialized = true;
        }
    }
}
