using System;
using Shared.Kernel;

namespace Shared.Time
{
    public sealed class ClockAdapter : IClockPort
    {
        public DateTime UtcNow => DateTime.UtcNow;

        public EntityId NewId() => EntityId.New();
    }
}
