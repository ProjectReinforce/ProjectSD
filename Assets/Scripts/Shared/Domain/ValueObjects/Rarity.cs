namespace SwDreams.Shared.Domain.ValueObjects
{
    /// <summary>
    /// 4등급 체계 공용 enum.
    /// 무기/능력치/혼돈 스킬/정수 드랍 등급 선정에 공통 사용.
    /// 순수 C# — UnityEngine/Photon 의존 금지.
    /// </summary>
    public enum Rarity
    {
        Common = 0,
        Rare = 1,
        Epic = 2,
        Legendary = 3
    }
}
