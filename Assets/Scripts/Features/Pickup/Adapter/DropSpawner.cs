using System;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using SwDreams.Features.Pickup.Domain;
using SwDreams.Shared.Data;
using SwDreams.Shared.Domain.ValueObjects;
using SwDreams.Shared.Managers;
using SwDreams.Shared.Network;

namespace SwDreams.Features.Pickup.Adapter
{
    /// <summary>
    /// 월드 드랍(정수/무기/자석/물약) 스폰 중앙 매니저. 호스트 전용 권위 + 클라 로컬 렌더.
    ///
    /// 흐름:
    /// 1. 호스트가 적 사망 시 <see cref="TrySpawnDrops"/> 호출.
    /// 2. 확률 + 등급 롤 → 배치 큐에 적재.
    /// 3. LateUpdate 에서 <see cref="DropSpawnBatch.EventCode"/> 로 RaiseEvent (Reliable/All).
    /// 4. 모든 클라 (호스트 포함) 가 수신해 로컬 풀에서 픽업 프리팹 스폰.
    ///
    /// 프리팹은 <see cref="pickupPrefabsByType"/> 배열에 PickupType enum 순서대로 할당.
    /// (index 0 = ExpOrb, 1 = Magnet, 2 = Potion, 3 = Essence, 4 = Weapon)
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class DropSpawner : MonoBehaviourPunCallbacks, IOnEventCallback
    {
        public static DropSpawner Instance { get; private set; }

        [Header("픽업 프리팹 (PickupType enum 순서 고정)")]
        [Tooltip("[0]ExpOrb [1]Magnet [2]Potion [3]Essence [4]Weapon. null 이면 해당 타입 드랍은 스폰 생략 + 경고.")]
        [SerializeField] private GameObject[] pickupPrefabsByType;

        [Header("풀 Prewarm")]
        [SerializeField] private int prewarmPerType = 16;

        private readonly List<(PickupType type, Vector2 pos, Rarity rarity, int dataIdHash)> dropQueue
            = new();
        private readonly System.Random rng = new System.Random();

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (pickupPrefabsByType == null) return;
            for (int i = 0; i < pickupPrefabsByType.Length; i++)
            {
                if (pickupPrefabsByType[i] != null)
                    PoolManager.Instance?.Prewarm(pickupPrefabsByType[i], prewarmPerType);
            }
        }

        // ===== 호스트: 드랍 결정 + 큐 적재 =====

        /// <summary>
        /// 호스트 전용. 적 사망 위치에서 DropTable 규칙에 따라 확률 롤 + 큐 적재.
        /// table 이 null 이면 no-op.
        /// </summary>
        public void TrySpawnDrops(Vector2 position, EnemyDropTable table, bool isElite)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (table == null) return;

            // 정수: 엘리트 전용
            if (isElite && Roll(table.essenceChance))
            {
                Rarity r = RarityWeightedRoller.Roll(table.essenceRarityWeights, rng);
                dropQueue.Add((PickupType.Essence, position, r, 0));
            }

            // 무기
            if (Roll(table.weaponChance))
            {
                Rarity r = RarityWeightedRoller.Roll(table.weaponRarityWeights, rng);
                dropQueue.Add((PickupType.Weapon, position, r, 0));
            }

            // 자석 / 물약 — 등급 개념 없음 → Common
            if (Roll(table.magnetChance))
                dropQueue.Add((PickupType.Magnet, position, Rarity.Common, 0));

            if (Roll(table.potionChance))
                dropQueue.Add((PickupType.Potion, position, Rarity.Common, 0));
        }

        private bool Roll(float chance)
        {
            if (chance <= 0f) return false;
            if (chance >= 1f) return true;
            return rng.NextDouble() < chance;
        }

        private void LateUpdate()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            FlushDropQueue();
        }

        private void FlushDropQueue()
        {
            if (dropQueue.Count == 0) return;

            int stride = DropSpawnBatch.Stride;
            float[] batch = new float[dropQueue.Count * stride];
            for (int i = 0; i < dropQueue.Count; i++)
            {
                var d = dropQueue[i];
                batch[i * stride + DropSpawnBatch.IdxType]       = (int)d.type;
                batch[i * stride + DropSpawnBatch.IdxPosX]       = d.pos.x;
                batch[i * stride + DropSpawnBatch.IdxPosY]       = d.pos.y;
                batch[i * stride + DropSpawnBatch.IdxRarity]     = (int)d.rarity;
                batch[i * stride + DropSpawnBatch.IdxDataIdHash] = d.dataIdHash;
            }

            PhotonNetwork.RaiseEvent(
                DropSpawnBatch.EventCode,
                batch,
                new RaiseEventOptions { Receivers = ReceiverGroup.All },
                SendOptions.SendReliable);

            dropQueue.Clear();
        }

        // ===== 클라 + 호스트: 수신 후 로컬 스폰 =====

        public void OnEvent(EventData photonEvent)
        {
            if (photonEvent.Code != DropSpawnBatch.EventCode) return;
            if (!(photonEvent.CustomData is float[] batch)) return;

            int stride = DropSpawnBatch.Stride;
            for (int i = 0; i + stride - 1 < batch.Length; i += stride)
            {
                int typeInt = (int)batch[i + DropSpawnBatch.IdxType];
                Vector2 pos = new Vector2(
                    batch[i + DropSpawnBatch.IdxPosX],
                    batch[i + DropSpawnBatch.IdxPosY]);
                int rarityInt = (int)batch[i + DropSpawnBatch.IdxRarity];
                int dataIdHash = (int)batch[i + DropSpawnBatch.IdxDataIdHash];

                SpawnPickupLocal((PickupType)typeInt, pos, (Rarity)rarityInt, dataIdHash);
            }
        }

        private void SpawnPickupLocal(PickupType type, Vector2 pos, Rarity rarity, int dataIdHash)
        {
            GameObject prefab = GetPrefab(type);
            if (prefab == null)
            {
                Debug.LogWarning($"[DropSpawner] {type} 프리팹 미등록 — 드랍 스킵. " +
                                 "Inspector 의 pickupPrefabsByType 배열에 할당 필요.");
                return;
            }

            GameObject obj = PoolManager.Instance?.Get(prefab);
            if (obj == null) return;

            var pickup = obj.GetComponent<PickupItemBase>();
            if (pickup == null)
            {
                Debug.LogError($"[DropSpawner] {prefab.name} 에 PickupItemBase 컴포넌트 없음.");
                PoolManager.Instance?.Return(obj);
                return;
            }

            pickup.Initialize(pos);
        }

        private GameObject GetPrefab(PickupType type)
        {
            if (pickupPrefabsByType == null) return null;
            int idx = (int)type;
            if (idx < 0 || idx >= pickupPrefabsByType.Length) return null;
            return pickupPrefabsByType[idx];
        }

        // ===== 유틸 =====

        /// <summary>
        /// 호스트 마이그레이션 시 호출. 드랍 큐 정리.
        /// 월드에 이미 떨어진 픽업은 PickupItemBase 가 각자 관리.
        /// </summary>
        public void ResetForMigration()
        {
            dropQueue.Clear();
        }
    }
}
