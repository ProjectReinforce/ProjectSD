using UnityEngine;
using SwDreams.Features.Pickup.Domain;
using SwDreams.Shared.Domain.Interfaces;
using SwDreams.Shared.Domain.ValueObjects;

namespace SwDreams.Features.Pickup.Adapter
{
    /// <summary>
    /// 물약 픽업. 획득자 본인만 즉시 HP 를 회복.
    /// 호스트 권위는 PickupItemBase.OnTriggerEnter2D 에서 이미 보장 — 이 훅은 호스트에서만 호출.
    /// Heal 은 PlayerHealth(IHealable) 가 내부적으로 RPC 전파.
    ///
    /// 프리팹 구성:
    /// - PotionPickup (이 스크립트)
    /// - Collider2D (isTrigger = true)
    /// - Rigidbody2D (Kinematic)
    /// - SpriteRenderer
    /// </summary>
    public class PotionPickup : PickupItemBase
    {
        [Header("물약 수치")]
        [SerializeField, Tooltip("회복량(고정값). 차후 비율 기반으로 바꿀 경우 필드 교체.")]
        private int healAmount = 30;

        private void Reset()
        {
            itemId = "potion";
            type = PickupType.Potion;
            rarity = Rarity.Common;
        }

        protected override void OnPickedUpByPlayer(GameObject playerObj)
        {
            var healable = playerObj.GetComponent<IHealable>();
            healable?.Heal(healAmount);
        }
    }
}
