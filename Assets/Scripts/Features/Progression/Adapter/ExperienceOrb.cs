using UnityEngine;
using SwDreams.Features.Pickup.Adapter;
using SwDreams.Features.Pickup.Domain;
using SwDreams.Shared.Domain.ValueObjects;
using SwDreams.Shared.Managers;

namespace SwDreams.Features.Progression.Adapter
{
    /// <summary>
    /// 경험치 오브. PickupItemBase 를 상속하여 자석 흡수/호스트 판정을 베이스에 위임.
    /// 획득 시 팀 공유 경험치를 호스트가 AddExp 로 누적.
    ///
    /// 모든 클라이언트에서 로컬 생성 (PhotonView 없음).
    /// SpawnManager.activeOrbs 상한 추적을 위해 OnReturnToPool 에서 알림 유지.
    ///
    /// 프리팹 구성:
    /// - ExperienceOrb (이 스크립트, PickupItemBase 상속)
    /// - CircleCollider2D (isTrigger = true)
    /// - Rigidbody2D (Kinematic)
    /// - SpriteRenderer
    /// </summary>
    public class ExperienceOrb : PickupItemBase
    {
        private int expValue;

        public void Initialize(Vector2 position, int exp)
        {
            base.Initialize(position);
            expValue = exp;
            itemId = "exp_orb";
            type = PickupType.ExpOrb;
            rarity = Rarity.Common;
        }

        protected override void OnPickedUpByPlayer(GameObject playerObj)
        {
            GameManager.Instance?.AddExp(expValue);
            Debug.Log($"[ExperienceOrb] 획득! +{expValue} EXP");
        }

        public override void OnReturnToPool()
        {
            // SpawnManager.activeOrbs FIFO 상한 추적에서 제거 (이미 제거됐으면 no-op)
            SpawnManager.Instance?.OnExpOrbReturned(this);
            base.OnReturnToPool();
        }
    }
}
