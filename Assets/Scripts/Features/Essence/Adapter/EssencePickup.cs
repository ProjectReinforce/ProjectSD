using UnityEngine;
using SwDreams.Features.Essence.Adapter.Data;
using SwDreams.Features.Essence.Domain;
using SwDreams.Features.Pickup.Adapter;
using SwDreams.Features.Pickup.Domain;
using SwDreams.Shared.Domain.ValueObjects;

namespace SwDreams.Features.Essence.Adapter
{
    /// <summary>
    /// 속성 정수 픽업. 엘리트 적 사망 시 DropSpawner 가 스폰.
    ///
    /// 속성(Ice/Fire/Lightning) 정보는 DropSpawner 가
    /// <see cref="InitializeEssence"/> 로 전달 (배치 RPC 의 dataIdHash 필드 경유).
    /// 획득자 2슬롯이 꽉 찬 경우 줍기 차단 — 월드에 그대로 남는다.
    ///
    /// 프리팹 구성:
    /// - EssencePickup (이 스크립트)
    /// - Collider2D (isTrigger = true)
    /// - Rigidbody2D (Kinematic)
    /// - SpriteRenderer (visual 필드 또는 자동 탐색)
    /// </summary>
    public class EssencePickup : PickupItemBase
    {
        [SerializeField] private SpriteRenderer visual;

        private EssenceType essenceType;
        private EssenceDatabase database;

        public EssenceType EssenceType => essenceType;

        /// <summary>접촉 즉시 획득 대신 Space 상호작용으로 획득.</summary>
        public override bool RequiresInteraction => true;

        public override string PromptActionLabel => "정수 획득";

        /// <summary>
        /// DropSpawner 가 풀에서 꺼낸 뒤 호출. 기본 Initialize 대신 이 경로로 초기화.
        /// 속성에 따른 색/스프라이트 틴트 적용.
        /// </summary>
        public void InitializeEssence(Vector2 position, EssenceType type, EssenceDatabase db)
        {
            base.Initialize(position);
            essenceType = type;
            database = db;
            itemId = $"essence_{type.ToString().ToLower()}";
            this.type = PickupType.Essence;
            rarity = Rarity.Common;

            ApplyVisual();
        }

        private void ApplyVisual()
        {
            if (visual == null) visual = GetComponent<SpriteRenderer>();
            if (visual == null || database == null) return;

            var data = database.GetByType(essenceType);
            if (data == null) return;

            if (data.icon != null) visual.sprite = data.icon;
            visual.color = data.iconColor;
        }

        public override bool CanBePickedUpBy(GameObject playerObj)
        {
            var inv = playerObj.GetComponentInChildren<PlayerEssenceInventory>();
            return inv == null || inv.CanEquip;
        }

        protected override void OnPickedUpByPlayer(GameObject playerObj)
        {
            var inv = playerObj.GetComponentInChildren<PlayerEssenceInventory>();
            if (inv == null || !inv.CanEquip) return;
            inv.RequestEquip(essenceType);
        }
    }
}
