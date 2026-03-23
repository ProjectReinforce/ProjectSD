using System.Collections.Generic;
using Shared.Kernel;

namespace Features.Skill.Domain
{
    public sealed class CooldownTracker
    {
        private readonly Dictionary<string, float> _lastCastTimes =
            new Dictionary<string, float>();

        public void RecordCast(DomainEntityId skillId, float time)
        {
            _lastCastTimes[skillId.Value] = time;
        }

        public void ClearCooldown(DomainEntityId skillId)
        {
            _lastCastTimes.Remove(skillId.Value);
        }

        public float GetLastCastTime(DomainEntityId skillId)
        {
            return _lastCastTimes.TryGetValue(skillId.Value, out var time) ? time : float.NegativeInfinity;
        }
    }
}
