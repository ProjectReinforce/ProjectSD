namespace SwDreams.Features.Unlock.Domain
{
    /// <summary>
    /// 언락 가능한 컨텐츠 종류 (meta-unlock.md §3).
    /// MVP: Skill / Weapon / Character / RefreshCharge.
    /// Cosmetic 은 슬롯만 예약 (시스템 자체는 별도, 본 시스템 범위 밖).
    /// </summary>
    public enum UnlockableType
    {
        Skill = 0,
        Weapon = 1,
        Character = 2,
        RefreshCharge = 3,
        Cosmetic = 4,
    }
}
