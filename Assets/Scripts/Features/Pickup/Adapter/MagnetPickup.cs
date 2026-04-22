using UnityEngine;
using Photon.Pun;
using SwDreams.Features.Pickup.Domain;
using SwDreams.Shared.Domain.ValueObjects;

namespace SwDreams.Features.Pickup.Adapter
{
    /// <summary>
    /// 자석 픽업. 획득자가 현재 맵 위 모든 PickupItemBase 를 즉시 끌어당긴다.
    /// 브로드캐스트는 DropSpawner 가 담당 (PhotonView 허브).
    ///
    /// 프리팹 구성:
    /// - MagnetPickup (이 스크립트)
    /// - Collider2D (isTrigger = true)
    /// - Rigidbody2D (Kinematic)
    /// - SpriteRenderer
    /// </summary>
    public class MagnetPickup : PickupItemBase
    {
        private void Reset()
        {
            itemId = "magnet";
            type = PickupType.Magnet;
            rarity = Rarity.Common;
        }

        protected override void OnPickedUpByPlayer(GameObject playerObj)
        {
            var pv = playerObj.GetComponent<PhotonView>();
            int actor = (pv != null && pv.Owner != null) ? pv.Owner.ActorNumber : -1;
            if (actor < 0) return;

            DropSpawner.Instance?.RaiseMagnetActivated(actor);
        }
    }
}
