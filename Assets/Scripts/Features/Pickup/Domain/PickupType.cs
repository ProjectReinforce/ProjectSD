namespace SwDreams.Features.Pickup.Domain
{
    /// <summary>
    /// 월드에 드랍되는 픽업 아이템 종류. 순수 C#.
    ///
    /// StatBoost 는 드랍 대상이 아님(만렙 레벨업/퀘스트 보상 진입) → 제외.
    /// </summary>
    public enum PickupType
    {
        ExpOrb = 0,
        Magnet = 1,
        Potion = 2,
        Essence = 3,
        Weapon = 4
    }
}
