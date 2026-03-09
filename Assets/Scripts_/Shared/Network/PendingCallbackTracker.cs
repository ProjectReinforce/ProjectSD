using System;
using System.Collections.Generic;

namespace Shared.Network
{
    /// <summary>
    /// Tracks pending async request callbacks keyed by operation type.
    /// Correlates outgoing requests with incoming callbacks (e.g. Photon, HTTP).
    /// </summary>
    public sealed class PendingCallbackTracker<TOperation> where TOperation : struct, Enum
    {
        public readonly struct PendingCallback
        {
            public PendingCallback(Action onSuccess, Action<string> onFailure)
            {
                OnSuccess = onSuccess;
                OnFailure = onFailure;
            }

            public Action OnSuccess { get; }
            public Action<string> OnFailure { get; }
        }

        private readonly Dictionary<TOperation, PendingCallback> _pending = new();

        public bool IsPending(TOperation op) => _pending.ContainsKey(op);

        public void Set(TOperation op, Action onSuccess, Action<string> onFailure)
        {
            if (_pending.ContainsKey(op))
                throw new InvalidOperationException($"{op} request already pending.");
            _pending[op] = new PendingCallback(onSuccess, onFailure);
        }

        public PendingCallback Consume(TOperation op)
        {
            if (!_pending.TryGetValue(op, out var cb))
                throw new InvalidOperationException($"No pending {op} request.");
            _pending.Remove(op);
            return cb;
        }

        public void Clear(TOperation op) => _pending.Remove(op);
    }
}
