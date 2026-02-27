using System;

namespace SwDreams.Domain.Interfaces
{
    /// <summary>
    /// 데미지를 받을 수 있는 엔티티의 도메인 인터페이스.
    /// Unity 의존성 없는 순수 C#.
    /// 넉백 등 물리 처리는 Adapter 레이어에서 별도 처리.
    /// </summary>
    public interface IDamageable
    {
        int CurrentHP { get; }
        int MaxHP { get; }
        bool IsAlive { get; }
        void TakeDamage(int damage);

        event Action<int, int> OnHealthChanged; // current, max
        event Action OnDied;
    }
}
