namespace SwDreams.Shared.Domain.Interfaces
{
    /// <summary>
    /// 체력 회복 대상 포트. 순수 C#.
    ///
    /// Pickup(물약/힐) 이 Character/Adapter 의 PlayerHealth 를 직접 참조하지 않도록
    /// Feature 경계를 넘어 공유되는 얇은 인터페이스.
    /// 구현체(PlayerHealth)는 호스트 권위 + RPC 전파를 자체 처리.
    /// </summary>
    public interface IHealable
    {
        void Heal(int amount);
    }
}
