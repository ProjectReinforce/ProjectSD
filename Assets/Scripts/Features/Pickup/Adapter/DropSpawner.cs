using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using SwDreams.Features.Essence.Adapter;
using SwDreams.Features.Essence.Adapter.Data;
using SwDreams.Features.Essence.Domain;
using SwDreams.Features.Pickup.Domain;
using SwDreams.Features.Weapon.Adapter;
using SwDreams.Features.Weapon.Adapter.Data;
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
    /// ExpOrb 는 DropSpawner 경로가 아니라 SpawnManager 가 직접 관리 — 100% 드랍이라 확률 롤 불필요.
    /// 따라서 이 컴포넌트는 Magnet/Potion/Essence/Weapon 4종만 담당.
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class DropSpawner : MonoBehaviourPunCallbacks, IOnEventCallback
    {
        public static DropSpawner Instance { get; private set; }

        [Header("픽업 프리팹")]
        [Tooltip("자석. null 이면 해당 타입 드랍은 스폰 생략 + 경고.")]
        [SerializeField] private GameObject magnetPrefab;

        [Tooltip("물약. null 이면 스폰 생략 + 경고.")]
        [SerializeField] private GameObject potionPrefab;

        [Tooltip("정수. Phase 3 이후. null 이면 스폰 생략 + 경고.")]
        [SerializeField] private GameObject essencePrefab;

        [Tooltip("무기. Phase 4 이후. null 이면 스폰 생략 + 경고.")]
        [SerializeField] private GameObject weaponPrefab;

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
            PrewarmIfSet(magnetPrefab);
            PrewarmIfSet(potionPrefab);
            PrewarmIfSet(essencePrefab);
            PrewarmIfSet(weaponPrefab);
        }

        private void PrewarmIfSet(GameObject prefab)
        {
            if (prefab != null)
                PoolManager.Instance?.Prewarm(prefab, prewarmPerType);
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

            // 정수: 엘리트 전용. 등급 개념 없음 — 속성 타입(Ice/Fire/Lightning) 가중치로 롤.
            // dataIdHash 자리에 속성 인덱스(0=Ice, 1=Fire, 2=Lightning)를 저장.
            // rarity 자리는 Common 고정 (정수는 등급 체계 미적용).
            if (isElite && Roll(table.essenceChance))
            {
                int essenceTypeIdx = RollWeightedIndex(table.essenceTypeWeights);
                dropQueue.Add((PickupType.Essence, ScatterFrom(position), Rarity.Common, essenceTypeIdx));
            }

            // 무기: 4등급 체계 롤.
            // 호스트가 WeaponDatabase 에서 실제 SO 1개를 샘플링 → 인덱스를 dataIdHash 에 담아 전송.
            // 인덱스는 WeaponDatabase.All 의 리스트 순서. 같은 SO 에셋을 모든 클라가 참조하므로 안정.
            if (Roll(table.weaponChance))
            {
                Rarity r = RarityWeightedRoller.Roll(table.weaponRarityWeights, rng);
                var db = GameManager.Instance?.WeaponDB;
                if (db != null)
                {
                    var picked = db.GetRandomByRarity(r, rng);
                    int idx = ResolveWeaponIndex(db, picked);
                    if (picked != null && idx >= 0)
                        dropQueue.Add((PickupType.Weapon, ScatterFrom(position), r, idx));
                    // 해당 등급에 무기가 없으면 드랍 생략 (null 스폰 방지).
                }
            }

            // 자석 / 물약 — 등급 개념 없음 → Common 고정.
            if (Roll(table.magnetChance))
                dropQueue.Add((PickupType.Magnet, ScatterFrom(position), Rarity.Common, 0));

            if (Roll(table.potionChance))
                dropQueue.Add((PickupType.Potion, ScatterFrom(position), Rarity.Common, 0));
        }

        /// <summary>
        /// 사망 위치를 중심으로 GameplayConfig.dropScatterRadius 내 임의 위치 반환.
        /// 호스트에서 결정되어 배치 RPC 로 전파되므로 클라 일관성 보장.
        /// </summary>
        private Vector2 ScatterFrom(Vector2 origin)
        {
            float radius = GameManager.Instance?.Config != null
                ? GameManager.Instance.Config.dropScatterRadius
                : 0.5f;
            if (radius <= 0f) return origin;

            float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
            float r = (float)rng.NextDouble() * radius;
            return origin + new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
        }

        /// <summary>
        /// weights 에 비례한 인덱스 롤. 총합이 0 이하면 0 반환.
        /// Rarity 가 아닌 임의 분류(정수 속성 등) 롤에 사용.
        /// </summary>
        private int RollWeightedIndex(float[] weights)
        {
            if (weights == null || weights.Length == 0) return 0;

            float total = 0f;
            for (int i = 0; i < weights.Length; i++)
                if (weights[i] > 0f) total += weights[i];
            if (total <= 0f) return 0;

            float pick = (float)rng.NextDouble() * total;
            float acc = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] <= 0f) continue;
                acc += weights[i];
                if (pick <= acc) return i;
            }
            return 0;
        }

        /// <summary>
        /// WeaponData 의 Database.All 내 인덱스. 없으면 -1.
        /// 캐시를 두지 않는 이유: 드랍 롤 빈도가 낮고 WeaponData 수가 적어 O(n) 탐색으로 충분.
        /// </summary>
        private static int ResolveWeaponIndex(WeaponDatabase db, WeaponData data)
        {
            if (db == null || data == null) return -1;
            var all = db.All;
            for (int i = 0; i < all.Count; i++)
                if (ReferenceEquals(all[i], data)) return i;
            return -1;
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
                                 "Inspector 의 해당 프리팹 필드에 할당 필요.");
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

            // Essence 는 dataIdHash 를 속성(EssenceType) 으로 해석해 전용 초기화 경로 사용.
            // EssenceDatabase 는 GameManager 가 단일 소유 (SSOT).
            if (type == PickupType.Essence && pickup is EssencePickup essence)
            {
                var db = GameManager.Instance?.EssenceDB;
                essence.InitializeEssence(pos, (EssenceType)dataIdHash, db);
            }
            else if (type == PickupType.Weapon && pickup is WeaponPickup weapon)
            {
                // dataIdHash = WeaponDatabase.All 내 인덱스. 동일 SO 참조라 모든 클라가 같은 WeaponData 해결.
                var db = GameManager.Instance?.WeaponDB;
                WeaponData data = null;
                if (db != null && dataIdHash >= 0 && dataIdHash < db.All.Count)
                    data = db.All[dataIdHash];
                weapon.InitializeWeapon(pos, data);
            }
            else
            {
                pickup.Initialize(pos);
            }
        }

        private GameObject GetPrefab(PickupType type)
        {
            switch (type)
            {
                case PickupType.Magnet:  return magnetPrefab;
                case PickupType.Potion:  return potionPrefab;
                case PickupType.Essence: return essencePrefab;
                case PickupType.Weapon:  return weaponPrefab;
                // ExpOrb 는 DropSpawner 관리 대상 아님 — SpawnManager 가 직접 스폰.
                case PickupType.ExpOrb:
                default:
                    return null;
            }
        }

        // ===== 자석(Magnet) 효과 브로드캐스트 =====

        /// <summary>
        /// 호스트 전용. 자석 픽업 발동 시 모든 클라에 RPC 를 보내
        /// 맵의 모든 PickupItemBase 를 해당 플레이어에게 즉시 끌어당긴다.
        /// actorNumber 는 자석을 획득한 플레이어의 Photon ActorNumber.
        /// </summary>
        public void RaiseMagnetActivated(int actorNumber)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (photonView == null || photonView.ViewID == 0)
            {
                Debug.LogError("[DropSpawner] PhotonView ViewID 미할당! 씬 오브젝트면 Scene 저장 후 재시작.");
                return;
            }
            photonView.RPC(nameof(RPC_ActivateMagnet), RpcTarget.All, actorNumber);
        }

        [PunRPC]
        private void RPC_ActivateMagnet(int actorNumber)
        {
            // RPC 와 DropSpawnBatch(RaiseEvent) 는 서로 다른 Photon 채널이라
            // 동일 프레임에 자석 발동 + 적 대량 사망이 쌓이면 자석이 배치 스폰보다 먼저 도착할 수 있다.
            // 한 프레임 지연 후 픽업 스캔하여 "막 스폰된 오브"까지 포함.
            StartCoroutine(ApplyMagnetNextFrame(actorNumber));
        }

        private IEnumerator ApplyMagnetNextFrame(int actorNumber)
        {
            yield return null;

            Transform target = FindPlayerByActor(actorNumber);
            if (target == null) yield break;

            // 자석은 경험치 오브만 끌어온다. 다른 자석/물약/정수/무기까지 끌어오면
            // 자석 연쇄 발동 등 의도치 않은 연쇄 효과가 발생할 수 있음.
            var pickups = FindObjectsByType<PickupItemBase>(FindObjectsSortMode.None);
            for (int i = 0; i < pickups.Length; i++)
            {
                var p = pickups[i];
                if (p == null) continue;
                if (!p.gameObject.activeInHierarchy) continue;
                if (p.Type != PickupType.ExpOrb) continue;
                p.ForceAttractTo(target);
            }
        }

        private Transform FindPlayerByActor(int actorNumber)
        {
            var players = GameObject.FindGameObjectsWithTag("Player");
            for (int i = 0; i < players.Length; i++)
            {
                var pv = players[i].GetComponent<PhotonView>();
                if (pv != null && pv.Owner != null && pv.Owner.ActorNumber == actorNumber)
                    return players[i].transform;
            }
            return null;
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
