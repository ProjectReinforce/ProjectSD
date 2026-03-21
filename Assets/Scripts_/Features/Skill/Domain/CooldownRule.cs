using Shared.Kernel;

namespace Features.Skill.Domain
{
    public static class CooldownRule
    {
        public static Result CanCast(Skill skill, float currentTime, CooldownTracker tracker)
        {
            var lastCastTime = tracker.GetLastCastTime(skill.Id);
            var elapsed = currentTime - lastCastTime;
            if (elapsed < skill.Spec.Cooldown)
            {
                return Result.Failure("Skill is on cooldown.");
            }

            return Result.Success();
        }
    }
}
