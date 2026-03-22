namespace Features.Skill.Application.Ports
{
    public interface ISkillNetworkCallbackPort
    {
        System.Action<SkillCastNetworkData> OnSkillCasted { set; }
    }
}
