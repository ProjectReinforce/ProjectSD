using System;
using Shared.Kernel;

namespace Features.Lobby.Application.Ports
{
    public interface IClockPort
    {
        DateTime UtcNow { get; }
        EntityId NewId();
    }
}
