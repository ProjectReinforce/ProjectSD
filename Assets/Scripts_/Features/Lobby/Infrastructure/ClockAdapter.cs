using System;
using Features.Lobby.Application.Ports;
using Shared.Kernel;

namespace Features.Lobby.Infrastructure
{
    public sealed class ClockAdapter : IClockPort
    {
        public DateTime UtcNow
        {
            get { return DateTime.UtcNow; }
        }

        public EntityId NewId()
        {
            return EntityId.New();
        }
    }
}
