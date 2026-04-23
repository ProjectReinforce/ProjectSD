using UnityEngine;
using Photon.Pun;
using SwDreams.Features.Pickup.Adapter;
using SwDreams.Features.Pickup.Domain;
using SwDreams.Features.Weapon.Adapter.Data;
using SwDreams.Shared.Domain.ValueObjects;

namespace SwDreams.Features.Weapon.Adapter
{
    /// <summary>
    /// 무기 픽업. DropSpawner 가 스폰 후 <see cref="InitializeWeapon"/> 로 초기화.
    ///
    /// 상호작용(Space) 기반 획득. 인벤토리가 꽉 찬 상태에서도 조합 가능하면 픽업 허용.
    /// 프롬프트 라벨/부가 정보는 런타임 인벤토리 상태(조합 여부)를 반영한다.
    /// </summary>
    public class WeaponPickup : PickupItemBase
    {
        [SerializeField] private SpriteRenderer visual;

        private WeaponData weaponData;

        public WeaponData Weapon => weaponData;

        public override bool RequiresInteraction => true;

        /// <summary>
        /// DropSpawner 가 풀에서 꺼낸 뒤 호출. 기본 Initialize 대신 이 경로로 초기화.
        /// </summary>
        public void InitializeWeapon(Vector2 position, WeaponData data)
        {
            base.Initialize(position);
            weaponData = data;
            this.type = PickupType.Weapon;

            if (data != null)
            {
                itemId = $"weapon_{data.weaponId}";
                rarity = data.rarity;
            }
            else
            {
                itemId = "weapon_unknown";
                rarity = Rarity.Common;
                Debug.LogWarning("[WeaponPickup] InitializeWeapon: WeaponData=null — " +
                                 "GameManager.WeaponDB 인덱스 해결 실패. 원인 후보: " +
                                 "GameManager Inspector 의 weaponDatabase 미할당 / " +
                                 "WeaponDatabase.weapons 리스트 비어있음 / null 엔트리.");
            }

            ApplyVisual();
        }

        private void ApplyVisual()
        {
            if (visual == null) visual = GetComponent<SpriteRenderer>();
            if (visual == null || weaponData == null) return;

            if (weaponData.icon != null) visual.sprite = weaponData.icon;
            visual.color = weaponData.iconColor;
        }

        /// <summary>
        /// 4 슬롯 가득 찼을 때도 조합이 가능하면 획득 허용 (조합 경로로 소비).
        /// </summary>
        public override bool CanBePickedUpBy(GameObject playerObj)
        {
            if (weaponData == null)
            {
                Debug.LogWarning("[WeaponPickup] CanBePickedUpBy=false — weaponData null. " +
                                 "InitializeWeapon 단계 경고 확인.");
                return false;
            }
            var inv = FindInventoryFromPlayerObj(playerObj);
            if (inv == null)
            {
                Debug.LogWarning($"[WeaponPickup] CanBePickedUpBy=false — PlayerWeaponInventory 미발견. " +
                                 $"playerObj='{playerObj.name}' (루트부터 탐색해도 없음). " +
                                 $"Player 프리팹 자식 어딘가에 컴포넌트 + PhotonView 필요.");
                return false;
            }
            bool accept = inv.CanAcceptOrCombine(weaponData);
            if (!accept)
            {
                Debug.Log($"[WeaponPickup] CanBePickedUpBy=false — 4슬롯 가득+조합 불가. " +
                          $"weaponId={weaponData.weaponId}, equipped={inv.EquippedCount}");
            }
            return accept;
        }

        protected override void OnPickedUpByPlayer(GameObject playerObj)
        {
            if (weaponData == null) return;
            var inv = FindInventoryFromPlayerObj(playerObj);
            if (inv == null) return;
            inv.RequestAddOrCombine(weaponData.weaponId);
        }

        /// <summary>
        /// playerObj 는 PlayerPickupInteractor 의 GO 또는 Player 루트 어느 쪽도 될 수 있음.
        /// 1) 자기 서브트리 → 2) Player 루트 탐색 후 전체 서브트리 → 3) 부모 체인. 순으로 찾음.
        /// </summary>
        private static PlayerWeaponInventory FindInventoryFromPlayerObj(GameObject playerObj)
        {
            if (playerObj == null) return null;

            var inv = playerObj.GetComponentInChildren<PlayerWeaponInventory>();
            if (inv != null) return inv;

            // Player 루트를 찾아 전체 서브트리 탐색
            Transform cur = playerObj.transform;
            Transform playerRoot = null;
            while (cur != null)
            {
                if (cur.CompareTag("Player")) { playerRoot = cur; break; }
                cur = cur.parent;
            }
            if (playerRoot != null)
            {
                inv = playerRoot.GetComponentInChildren<PlayerWeaponInventory>();
                if (inv != null) return inv;
            }

            return playerObj.GetComponentInParent<PlayerWeaponInventory>();
        }

        public override string PromptActionLabel
        {
            get
            {
                // 획득자 탐색은 여기선 하지 않는다 (BaseSide 호출 타이밍에 ownerPlayer 미확정).
                // PlayerPickupInteractor 가 CurrentTarget 기준으로 매 프레임 라벨을 읽어가며,
                // 이 시점엔 자기 자신만 알아서 "획득" 고정 라벨을 사용해도 UX 상 큰 문제 없음.
                // 조합 사실은 PromptExtraInfo 로 드러낸다.
                return "무기 획득";
            }
        }

        /// <summary>
        /// 로컬 플레이어 인벤토리를 조회해 "조합 결과 미리보기" 를 반환.
        /// 조합 불가 시 null. 원격 플레이어 인벤토리는 내가 볼 필요 없음.
        /// </summary>
        public override string PromptExtraInfo
        {
            get
            {
                if (weaponData == null) return null;
                var inv = FindLocalInventory();
                if (inv == null) return null;

                var preview = inv.PreviewCombineResult(weaponData);
                if (preview == null) return null;
                return $"조합 → {preview.displayName}";
            }
        }

        private static PlayerWeaponInventory FindLocalInventory()
        {
            var players = GameObject.FindGameObjectsWithTag("Player");
            for (int i = 0; i < players.Length; i++)
            {
                var pv = players[i].GetComponent<PhotonView>();
                if (pv == null || !pv.IsMine) continue;
                return players[i].GetComponentInChildren<PlayerWeaponInventory>();
            }
            return null;
        }
    }
}
