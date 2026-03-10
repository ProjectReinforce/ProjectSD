using Shared.Kernel;

namespace Features.Skill.Domain
{
    public static class CooldownRule
    {
        public static Result CanCast(Skill skill, float currentTime, float lastCastTime)
        {
            var elapsed = currentTime - lastCastTime;
            if (elapsed < skill.Spec.Cooldown)
            {
                return Result.Failure("Skill is on cooldown.");
            }

            return Result.Success();
        }
    }
}
