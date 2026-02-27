namespace SwDreams.Domain.Interfaces
{
    /// <summary>
    /// 오브젝트 풀에서 관리되는 엔티티의 생명주기 인터페이스.
    /// Enemy, Projectile, ExperienceOrb, 이펙트 등이 구현.
    /// </summary>
    public interface IPoolable
    {
        void OnSpawnFromPool();
        void OnReturnToPool();
    }
}
