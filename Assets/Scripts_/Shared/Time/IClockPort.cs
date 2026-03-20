using System;
using Shared.Kernel;

namespace Shared.Time
{
    public interface IClockPort
    {
        DateTime UtcNow { get; }
        DomainEntityId NewId();
    }
}
