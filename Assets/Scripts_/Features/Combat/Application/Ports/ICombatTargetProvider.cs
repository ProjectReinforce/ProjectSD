namespace Features.Combat.Application.Ports
{
    /// <summary>Combat 데미지 파이프라인에 참여하는 대상의 인터페이스.</summary>
    public interface ICombatTargetProvider
    {
        float GetDefense();
        float GetCurrentHealth();
        CombatTargetDamageResult ApplyDamage(float damage);
    }
}
