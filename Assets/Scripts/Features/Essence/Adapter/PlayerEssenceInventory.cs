using System;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using SwDreams.Features.Essence.Adapter.Data;
using SwDreams.Features.Essence.Domain;
using SwDreams.Features.Skill.Domain.ValueObjects;
using SwDreams.Shared.Domain.Interfaces;
using SwDreams.Shared.Managers;

namespace SwDreams.Features.Essence.Adapter
{
    /// <summary>
    /// 플레이어의 정수 보유/장착 인벤토리. 최대 2슬롯.
    ///
    /// 장착 판정은 호스트가 내리지만, 실제 런타임 효과 주입은 각 클라이언트가
    /// 자신의 SkillTriggerSystem 에 호출해야 한다 (스킬 인스턴스가 로컬 플레이어에만 존재).
    /// 따라서 RPC_Equip 은 AllBuffered 로 모든 클라 + 중도 참가자에게 전파.
    ///
    /// 중첩 규약:
    /// - source = "essence_{type}_{slotIndex}" (슬롯별 구분) — AddRuntimeEffect 덮어쓰기 우회.
    /// - 같은 속성 2개 장착 시:
    ///   1) 슬롯 0/1 각각 1스택 효과로 주입 → 핸들러가 독립 동작이면 자연 합산.
    ///   2) EssenceData.injectedEffectsStack2 가 비어있지 않으면 슬롯 1 제거 + 슬롯 0 을 Stack2 로 교체.
    ///      → 총 효과 = Stack2 1회분 (비선형 시너지).
    /// - 해제 시 "essence_" prefix 로 일괄 제거 가능 (RemoveByPrefix 호환 유지).
    ///
    /// Player 프리팹의 자식 GameObject 에 이 컴포넌트 부착.
    /// PhotonView 는 부모 Player 의 것을 공유 (Observed 아님 — RPC 용).
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class PlayerEssenceInventory : MonoBehaviourPun
    {
        public const int MaxSlots = 2;
        private const string SourcePrefix = "essence_";

        // EssenceDatabase 는 GameManager.Instance.EssenceDB (SSOT) 를 사용.
        private EssenceDatabase Database => GameManager.Instance?.EssenceDB;

        private readonly List<EssenceType> equipped = new List<EssenceType>();
        private ISkillRegistry skillRegistry;

        public IReadOnlyList<EssenceType> Equipped => equipped;
        public bool CanEquip => equipped.Count < MaxSlots;

        /// <summary>디버그용: 포트 연결 여부. false 이면 주입 실패.</summary>
        public bool IsSkillRegistryConnected => skillRegistry != null;

        /// <summary>장착 변경 시 발생 (HUD 바인딩용).</summary>
        public event Action OnEquippedChanged;

        private void Start()
        {
            EnsureSkillRegistry();

            if (skillRegistry != null)
                skillRegistry.OnSinkAdded += HandleSinkAdded;
        }

        private void OnDestroy()
        {
            if (skillRegistry != null)
                skillRegistry.OnSinkAdded -= HandleSinkAdded;
        }

        /// <summary>
        /// 호스트 전용 장착 요청. AllBuffered 로 중도 참가자까지 replay.
        /// </summary>
        public void RequestEquip(EssenceType type)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (!CanEquip) return;
            photonView.RPC(nameof(RPC_Equip), RpcTarget.AllBuffered, (int)type);
        }

        [PunRPC]
        private void RPC_Equip(int typeInt)
        {
            if (!CanEquip) return;

            var type = (EssenceType)typeInt;
            int slotIndex = equipped.Count; // 새로 추가되는 슬롯
            equipped.Add(type);

            // 1스택 주입
            InjectSlot(type, slotIndex);

            // 2스택 시너지 체크 — 같은 속성이 이미 있었고(slotIndex == 1), Stack2 정의가 있으면 교체.
            TryApplySynergy(type);

            OnEquippedChanged?.Invoke();
        }

        // ===== 슬롯별 주입/해제 =====

        /// <summary>
        /// 단일 슬롯의 정수 효과를 현재 보유 스킬 전체에 주입.
        /// </summary>
        private void InjectSlot(EssenceType type, int slotIndex)
        {
            EnsureSkillRegistry();
            if (skillRegistry == null)
            {
                Debug.LogWarning($"[PlayerEssenceInventory] InjectSlot({type}) 실패 — skillRegistry 포트 미연결.");
                return;
            }

            var data = Database?.GetByType(type);
            if (data == null) return;

            var effects = data.injectedEffects;
            if (effects == null || effects.Length == 0) return;

            string source = MakeSource(type, slotIndex);
            var sinks = skillRegistry.EffectSinks;
            for (int i = 0; i < sinks.Count; i++)
            {
                var sink = sinks[i];
                for (int j = 0; j < effects.Length; j++)
                    sink.AddRuntimeEffect(source, effects[j]);
            }
        }

        /// <summary>
        /// 특정 슬롯의 source 를 현재 보유 스킬 전체에서 제거.
        /// </summary>
        private void RemoveSlot(EssenceType type, int slotIndex)
        {
            if (skillRegistry == null) return;
            string source = MakeSource(type, slotIndex);
            var sinks = skillRegistry.EffectSinks;
            for (int i = 0; i < sinks.Count; i++)
                sinks[i].RemoveRuntimeEffects(source);
        }

        /// <summary>
        /// Stack2 시너지 조건 확인 후 효과 교체.
        /// 조건: equipped 끝 두 개가 같은 type + Stack2 정의 존재.
        /// 동작: 슬롯 1 효과 제거 + 슬롯 0 의 기존 효과 제거 후 Stack2 효과로 주입.
        /// </summary>
        private void TryApplySynergy(EssenceType addedType)
        {
            if (equipped.Count < 2) return;
            // 두 슬롯이 모두 같은 속성인지 (단순 예: equipped[0] == equipped[1])
            if (equipped[0] != equipped[1]) return;

            var data = Database?.GetByType(addedType);
            var stack2 = data?.injectedEffectsStack2;
            if (stack2 == null || stack2.Length == 0) return;

            EnsureSkillRegistry();
            if (skillRegistry == null) return;

            // 슬롯 1 효과 제거 (기본 1스택은 더 이상 독립 발동하지 않음)
            RemoveSlot(addedType, 1);

            // 슬롯 0 의 기본 효과 제거 후 Stack2 효과로 재주입
            RemoveSlot(addedType, 0);
            string source0 = MakeSource(addedType, 0);
            var sinks = skillRegistry.EffectSinks;
            for (int i = 0; i < sinks.Count; i++)
            {
                for (int j = 0; j < stack2.Length; j++)
                    sinks[i].AddRuntimeEffect(source0, stack2[j]);
            }
        }

        /// <summary>
        /// 새 스킬 획득 시 포트가 그 스킬의 sink 를 넘겨줌. 이미 장착된 정수 효과를 그 sink 에만 재주입.
        /// Stack2 시너지 상태도 고려.
        /// </summary>
        private void HandleSinkAdded(IRuntimeEffectSink sink)
        {
            if (sink == null) return;

            bool synergyActive = equipped.Count == 2 && equipped[0] == equipped[1]
                                 && HasStack2(equipped[0]);

            if (synergyActive)
            {
                // 슬롯 0 source 에 Stack2 효과만 주입 (슬롯 1 은 주입 안 함)
                var stack2 = Database.GetByType(equipped[0]).injectedEffectsStack2;
                string source0 = MakeSource(equipped[0], 0);
                for (int j = 0; j < stack2.Length; j++)
                    sink.AddRuntimeEffect(source0, stack2[j]);
            }
            else
            {
                // 각 슬롯 1스택 효과 주입
                for (int slot = 0; slot < equipped.Count; slot++)
                {
                    var data = Database?.GetByType(equipped[slot]);
                    var effects = data?.injectedEffects;
                    if (effects == null || effects.Length == 0) continue;

                    string source = MakeSource(equipped[slot], slot);
                    for (int j = 0; j < effects.Length; j++)
                        sink.AddRuntimeEffect(source, effects[j]);
                }
            }
        }

        // ===== 유틸 =====

        private bool HasStack2(EssenceType type)
        {
            var stack2 = Database?.GetByType(type)?.injectedEffectsStack2;
            return stack2 != null && stack2.Length > 0;
        }

        private static string MakeSource(EssenceType type, int slotIndex)
            => $"{SourcePrefix}{type.ToString().ToLower()}_{slotIndex}";

        private void EnsureSkillRegistry()
        {
            if (skillRegistry != null) return;

            // Player 루트 탐색 — 포트 구현체(SkillManager) 를 interface 로 찾음.
            Transform cur = transform;
            Transform playerRoot = null;
            while (cur != null)
            {
                if (cur.CompareTag("Player"))
                {
                    playerRoot = cur;
                    break;
                }
                cur = cur.parent;
            }

            if (playerRoot != null)
                skillRegistry = playerRoot.GetComponentInChildren<ISkillRegistry>();

            if (skillRegistry == null)
                skillRegistry = GetComponentInParent<ISkillRegistry>();
            if (skillRegistry == null)
                skillRegistry = GetComponentInChildren<ISkillRegistry>();
        }
    }
}
