using System;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using SwDreams.Features.Character.Domain.ValueObjects;
using SwDreams.Features.Weapon.Adapter.Data;
using SwDreams.Features.Weapon.Domain;
using SwDreams.Shared.Domain.Interfaces;
using SwDreams.Shared.Managers;

namespace SwDreams.Features.Weapon.Adapter
{
    /// <summary>
    /// 플레이어 무기 인벤토리. 최대 4 슬롯. 조합 레시피 + per-entry isUnique 지원.
    ///
    /// 장착 경로 (한 플레이어 기준):
    /// 1) 호스트가 <see cref="RequestAddOrCombine"/> 호출 (WeaponPickup.OnPickedUpByPlayer 에서).
    /// 2) 호스트가 조합 가능성 평가 → RPC_CombineWeapon(consumed, result) 또는 RPC_EquipWeapon(id) AllBuffered.
    /// 3) 모든 클라(+ 호스트) 가 같은 시퀀스로 장착 상태를 갱신.
    ///
    /// Source 네이밍 (엔트리별 — isUnique 에 따라 다름):
    ///   unique  : "weapon_{id}_u_e{entryIdx}"         — 슬롯 무관, 중복 장착해도 1회분 (AddOrReplace 교체)
    ///   !unique : "weapon_{id}_s{slotUid}_e{entryIdx}" — 슬롯별 독립 스택
    ///
    /// 슬롯 해제 로직:
    ///   - 비-유니크 엔트리: "weapon_{id}_s{slotUid}_" prefix 일괄 제거 (이 슬롯만)
    ///   - 유니크 엔트리: 같은 id 의 다른 슬롯이 남아있으면 유지, 마지막 복사본 제거 시에만 "weapon_{id}_u_" 제거
    ///
    /// slotUid 결정성: **호스트가 할당해 RPC 에 싣는다**. 모든 클라는 RPC 파라미터의 값을 그대로 사용.
    /// 호스트 마이그레이션 이후에는 새 호스트가 기존 `nextSlotUid` 를 알 수 없지만, 새로 발행하는
    /// slotUid 가 과거 값과 충돌해도 네임스페이스가 `{weaponId}_s{slotUid}_` 인 이상 "과거 슬롯이 이미 해제됐다면"
    /// 문제 없음. 같은 `weaponId + slotUid` 가 동시에 살아있지만 않으면 충돌은 논리적으로 발생 불가.
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class PlayerWeaponInventory : MonoBehaviourPun
    {
        public const int MaxSlots = 4;
        private const string WeaponPrefix = "weapon_";

        private struct EquippedSlot
        {
            public WeaponData data;
            public int slotUid;
        }

        private WeaponDatabase Database => GameManager.Instance?.WeaponDB;

        private readonly List<EquippedSlot> equipped = new List<EquippedSlot>();

        /// <summary>호스트만 allocate. 클라는 RPC 파라미터로 전달받은 값을 그대로 사용.</summary>
        private int nextSlotUid = 1;

        private IPlayerStatsMutator stats;
        private ISkillRegistry skillRegistry;

        /// <summary>장착 개수 (비할당 접근).</summary>
        public int EquippedCount => equipped.Count;

        /// <summary>장착된 WeaponData 만 공개 (slotUid 는 내부 구현).
        /// 매 호출마다 리스트를 할당하므로 Update 루프에서 사용 금지 — HUD 갱신 시점에만 호출.</summary>
        public IReadOnlyList<WeaponData> Equipped
        {
            get
            {
                var list = new List<WeaponData>(equipped.Count);
                for (int i = 0; i < equipped.Count; i++) list.Add(equipped[i].data);
                return list;
            }
        }

        public bool HasFreeSlot => equipped.Count < MaxSlots;

        /// <summary>장착/해제/조합 후 발생 (HUD 바인딩용).</summary>
        public event Action OnEquippedChanged;

        private void Start()
        {
            EnsureReferences();

            if (skillRegistry != null)
                skillRegistry.OnSinkAdded += HandleSinkAdded;
        }

        private void OnDestroy()
        {
            if (skillRegistry != null)
                skillRegistry.OnSinkAdded -= HandleSinkAdded;
        }

        // ===== 픽업 측 진입점 =====

        public bool CanAcceptOrCombine(WeaponData incoming)
        {
            if (incoming == null) return false;
            if (HasFreeSlot) return true;
            return PreviewCombineResult(incoming) != null;
        }

        public WeaponData PreviewCombineResult(WeaponData incoming)
        {
            if (incoming == null || Database == null) return null;
            var combined = BuildCombinedIds(incoming.weaponId);
            return FindFirstMatchingRecipe(incoming.weaponId, combined, out _);
        }

        public void RequestAddOrCombine(string weaponId)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (string.IsNullOrEmpty(weaponId)) return;
            if (Database == null) return;

            var incoming = Database.GetById(weaponId);
            if (incoming == null) return;

            // 1) 조합 시도
            var combined = BuildCombinedIds(weaponId);
            var recipeResult = FindFirstMatchingRecipe(weaponId, combined, out var consumedIds);
            if (recipeResult != null)
            {
                int resultSlotUid = nextSlotUid++;
                photonView.RPC(nameof(RPC_CombineWeapon), RpcTarget.AllBuffered,
                    consumedIds, recipeResult.weaponId, resultSlotUid);
                return;
            }

            // 2) 빈 슬롯 장착
            if (HasFreeSlot)
            {
                int slotUid = nextSlotUid++;
                photonView.RPC(nameof(RPC_EquipWeapon), RpcTarget.AllBuffered, weaponId, slotUid);
                return;
            }

            // 조합도 안 되고 슬롯도 없으면 무시 (CanBePickedUpBy 에서 차단됐어야 함).
        }

        [PunRPC]
        private void RPC_EquipWeapon(string weaponId, int slotUid)
        {
            var data = Database?.GetById(weaponId);
            if (data == null) return;
            if (!HasFreeSlot) return;

            equipped.Add(new EquippedSlot { data = data, slotUid = slotUid });

            InjectModifiers(data, slotUid);
            InjectTriggers(data, slotUid);
            if (stats != null) stats.Recalculate();

            OnEquippedChanged?.Invoke();
        }

        [PunRPC]
        private void RPC_CombineWeapon(string[] consumedIds, string resultId, int resultSlotUid)
        {
            if (consumedIds == null || consumedIds.Length == 0) return;

            for (int i = 0; i < consumedIds.Length; i++)
            {
                string cid = consumedIds[i];
                int idx = FindEquippedIndexById(cid);
                if (idx < 0) continue;

                var slot = equipped[idx];
                equipped.RemoveAt(idx);
                RevokeSlot(slot);
            }

            var result = Database?.GetById(resultId);
            if (result != null)
            {
                equipped.Add(new EquippedSlot { data = result, slotUid = resultSlotUid });
                InjectModifiers(result, resultSlotUid);
                InjectTriggers(result, resultSlotUid);
            }

            if (stats != null) stats.Recalculate();
            OnEquippedChanged?.Invoke();
        }

        // ===== 주입/회수 =====

        /// <summary>
        /// 슬롯 해제 시 호출. 유니크 엔트리는 마지막 사본일 때만 제거.
        /// </summary>
        private void RevokeSlot(EquippedSlot slot)
        {
            EnsureReferences();

            var data = slot.data;
            if (data == null) return;

            // 1) 비-유니크 엔트리: 슬롯 고유 prefix 로 일괄 제거
            string slotPrefix = MakeSlotPrefix(data.weaponId, slot.slotUid);
            stats?.RemoveModifiersByPrefix(slotPrefix);
            RemoveTriggersByPrefix(slotPrefix);

            // 2) 유니크 엔트리: 남은 사본 있으면 유지, 없으면 제거
            bool hasOtherCopy = false;
            for (int i = 0; i < equipped.Count; i++)
            {
                if (equipped[i].data != null && equipped[i].data.weaponId == data.weaponId)
                {
                    hasOtherCopy = true;
                    break;
                }
            }
            if (!hasOtherCopy)
            {
                string uniqPrefix = MakeUniquePrefix(data.weaponId);
                stats?.RemoveModifiersByPrefix(uniqPrefix);
                RemoveTriggersByPrefix(uniqPrefix);
            }
        }

        private void InjectModifiers(WeaponData data, int slotUid)
        {
            EnsureReferences();
            if (stats == null || data?.statEntries == null) return;

            for (int i = 0; i < data.statEntries.Length; i++)
            {
                var e = data.statEntries[i];
                string source = MakeEntrySource(data.weaponId, slotUid, i, e.isUnique);
                stats.AddModifier(new StatModifier(source, e.statType, e.op, e.value));
            }
        }

        private void InjectTriggers(WeaponData data, int slotUid)
        {
            EnsureReferences();
            if (skillRegistry == null || data?.triggerEntries == null) return;

            var sinks = skillRegistry.EffectSinks;
            for (int s = 0; s < sinks.Count; s++)
            {
                var sink = sinks[s];
                for (int i = 0; i < data.triggerEntries.Length; i++)
                {
                    var t = data.triggerEntries[i];
                    string source = MakeEntrySource(data.weaponId, slotUid, i, t.isUnique);
                    sink.AddRuntimeEffect(source, t.effect);
                }
            }
        }

        private void RemoveTriggersByPrefix(string prefix)
        {
            if (skillRegistry == null) return;
            var sinks = skillRegistry.EffectSinks;
            for (int i = 0; i < sinks.Count; i++)
                sinks[i].RemoveByPrefix(prefix);
        }

        /// <summary>신규 스킬 획득 시 포트가 그 sink 를 전달. 기존 장착 무기의 트리거 효과를 재주입.</summary>
        private void HandleSinkAdded(IRuntimeEffectSink sink)
        {
            if (sink == null) return;

            // 유니크 엔트리는 id 당 1 번만 주입되도록 추적.
            var injectedUniqueSources = new HashSet<string>();

            for (int s = 0; s < equipped.Count; s++)
            {
                var slot = equipped[s];
                var data = slot.data;
                if (data?.triggerEntries == null) continue;

                for (int i = 0; i < data.triggerEntries.Length; i++)
                {
                    var t = data.triggerEntries[i];
                    string source = MakeEntrySource(data.weaponId, slot.slotUid, i, t.isUnique);
                    if (t.isUnique)
                    {
                        if (!injectedUniqueSources.Add(source)) continue;
                    }
                    sink.AddRuntimeEffect(source, t.effect);
                }
            }
        }

        // ===== 조합 매칭 =====

        private List<string> BuildCombinedIds(string incomingId)
        {
            var list = new List<string>(equipped.Count + 1);
            for (int i = 0; i < equipped.Count; i++)
            {
                if (equipped[i].data != null) list.Add(equipped[i].data.weaponId);
            }
            list.Add(incomingId);
            return list;
        }

        private WeaponData FindFirstMatchingRecipe(
            string incomingId,
            List<string> combinedIds,
            out string[] consumedFromEquipped)
        {
            consumedFromEquipped = null;
            if (Database == null) return null;

            foreach (var candidate in Database.All)
            {
                if (candidate == null) continue;
                var recipe = candidate.combineRecipe;
                if (!recipe.IsValid) continue;

                if (!ContainsId(recipe.inputWeaponIds, incomingId)) continue;
                if (!IsMultisetSubset(recipe.inputWeaponIds, combinedIds)) continue;

                consumedFromEquipped = SubtractOne(recipe.inputWeaponIds, incomingId);
                return candidate;
            }
            return null;
        }

        private static bool ContainsId(string[] arr, string id)
        {
            if (arr == null) return false;
            for (int i = 0; i < arr.Length; i++)
                if (arr[i] == id) return true;
            return false;
        }

        private static bool IsMultisetSubset(string[] required, List<string> available)
        {
            if (required == null || required.Length == 0) return false;
            var reqCount = new Dictionary<string, int>();
            for (int i = 0; i < required.Length; i++)
            {
                if (string.IsNullOrEmpty(required[i])) return false;
                reqCount[required[i]] = reqCount.TryGetValue(required[i], out var c) ? c + 1 : 1;
            }

            foreach (var kv in reqCount)
            {
                int availableCount = 0;
                for (int i = 0; i < available.Count; i++)
                    if (available[i] == kv.Key) availableCount++;
                if (availableCount < kv.Value) return false;
            }
            return true;
        }

        private static string[] SubtractOne(string[] required, string incomingId)
        {
            if (required == null || required.Length == 0) return Array.Empty<string>();
            bool removed = false;
            var list = new List<string>(required.Length);
            for (int i = 0; i < required.Length; i++)
            {
                if (!removed && required[i] == incomingId)
                {
                    removed = true;
                    continue;
                }
                list.Add(required[i]);
            }
            return list.ToArray();
        }

        private int FindEquippedIndexById(string weaponId)
        {
            for (int i = 0; i < equipped.Count; i++)
            {
                if (equipped[i].data != null && equipped[i].data.weaponId == weaponId) return i;
            }
            return -1;
        }

        // ===== Source 네이밍 =====

        private static string MakeUniquePrefix(string weaponId)
            => $"{WeaponPrefix}{weaponId}_u_";

        private static string MakeSlotPrefix(string weaponId, int slotUid)
            => $"{WeaponPrefix}{weaponId}_s{slotUid}_";

        private static string MakeEntrySource(string weaponId, int slotUid, int entryIdx, bool isUnique)
            => isUnique
                ? $"{WeaponPrefix}{weaponId}_u_e{entryIdx}"
                : $"{WeaponPrefix}{weaponId}_s{slotUid}_e{entryIdx}";

        // ===== 참조 해결 =====

        private void EnsureReferences()
        {
            if (stats != null && skillRegistry != null) return;

            Transform cur = transform;
            Transform playerRoot = null;
            while (cur != null)
            {
                if (cur.CompareTag("Player")) { playerRoot = cur; break; }
                cur = cur.parent;
            }

            if (playerRoot != null)
            {
                if (stats == null)          stats          = playerRoot.GetComponentInChildren<IPlayerStatsMutator>();
                if (skillRegistry == null)  skillRegistry  = playerRoot.GetComponentInChildren<ISkillRegistry>();
            }

            if (stats == null)          stats          = GetComponentInParent<IPlayerStatsMutator>();
            if (skillRegistry == null)  skillRegistry  = GetComponentInParent<ISkillRegistry>();
            if (stats == null)          stats          = GetComponentInChildren<IPlayerStatsMutator>();
            if (skillRegistry == null)  skillRegistry  = GetComponentInChildren<ISkillRegistry>();
        }
    }
}
