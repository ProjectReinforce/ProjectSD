using SwDreams.Shared.Domain.ValueObjects;

namespace SwDreams.Features.Pickup.Domain
{
    /// <summary>
    /// 월드 픽업 아이템의 식별 계약. 순수 C# — UnityEngine/Photon 의존 금지.
    ///
    /// 실제 MonoBehaviour 구현체는 Adapter 레이어의 PickupItemBase 에 둔다.
    /// 픽업 시점 행동(OnPickedUpByPlayer 등)은 Adapter 의 템플릿 메서드로 정의 —
    /// Domain 에는 식별용 프로퍼티만 노출.
    /// </summary>
    public interface IPickup
    {
        string ItemId { get; }
        PickupType Type { get; }
        Rarity Rarity { get; }
    }
}
