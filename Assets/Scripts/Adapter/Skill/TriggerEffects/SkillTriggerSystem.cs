using System.Collections.Generic;
using UnityEngine;
using SwDreams.Domain.ValueObjects;

namespace SwDreams.Adapter.Skill.TriggerEffects
{
    /// <summary>
    /// 스킬별 Trigger+Effect 관리 및 실행.
    /// 각 Skill 오브젝트의 자식 또는 동일 오브젝트에 부착.
    ///
    /// 기본 효과 (SO에서 정의) + 런타임 효과 (정수/무기/혼돈 등) 통합 관리.
    /// FireTrigger() 호출 시 등록된 모든 효과 중 매칭되는 것을 실행.
    ///
    /// 사용 흐름:
    /// 1. SkillManager.CreateSkillSlot()에서 Initialize(skillData.triggerEffects) 호출
    /// 2. 정수 장착 시 AddRuntimeEffect("essence_화염", ...) 
    /// 3. Projectile.OnHit에서 FireTrigger(OnHit, context) 호출
    /// 4. 정수 해제 시 RemoveRuntimeEffects("essence_화염")
    ///
    /// [Phase 7 리팩토링] Step 3-3
    /// </summary>
    public class SkillTriggerSystem : MonoBehaviour
    {
        // SO에서 로드된 기본 효과 (불변)
        private List<SkillTriggerEffect> baseEffects = new List<SkillTriggerEffect>();

        // 런타임에 추가된 효과 (정수/무기/혼돈 등)
        private List<RuntimeTriggerEffect> runtimeEffects = new List<RuntimeTriggerEffect>();

        // 공유 레지스트리 (모든 스킬이 동일 핸들러 사용)
        private static EffectActionRegistry sharedRegistry;

        // ===== 초기화 =====

        /// <summary>
        /// SkillData의 기본 트리거 효과로 초기화.
        /// SkillManager.CreateSkillSlot()에서 호출.
        /// </summary>
        public void Initialize(List<SkillTriggerEffect> effects)
        {
            baseEffects.Clear();
            if (effects != null)
                baseEffects.AddRange(effects);
        }

        // ===== 런타임 효과 관리 =====

        /// <summary>
        /// 런타임 효과 추가. 정수/무기/혼돈 등에서 호출.
        /// 동일 source+trigger+action 조합이면 교체.
        /// </summary>
        public void AddRuntimeEffect(string source, SkillTriggerEffect effect)
        {
            // 동일 조합 교체
            for (int i = 0; i < runtimeEffects.Count; i++)
            {
                var rt = runtimeEffects[i];
                if (rt.source == source &&
                    rt.effect.trigger == effect.trigger &&
                    rt.effect.action == effect.action)
                {
                    runtimeEffects[i] = new RuntimeTriggerEffect(source, effect);
                    return;
                }
            }

            runtimeEffects.Add(new RuntimeTriggerEffect(source, effect));
        }

        /// <summary>
        /// source가 일치하는 모든 런타임 효과 제거.
        /// </summary>
        /// <returns>제거된 효과 수</returns>
        public int RemoveRuntimeEffects(string source)
        {
            int removed = 0;
            for (int i = runtimeEffects.Count - 1; i >= 0; i--)
            {
                if (runtimeEffects[i].source == source)
                {
                    runtimeEffects.RemoveAt(i);
                    removed++;
                }
            }
            return removed;
        }

        /// <summary>
        /// source 접두사가 일치하는 모든 런타임 효과 제거.
        /// 예: RemoveByPrefix("essence_") → 모든 정수 효과 제거.
        /// </summary>
        public int RemoveByPrefix(string prefix)
        {
            int removed = 0;
            for (int i = runtimeEffects.Count - 1; i >= 0; i--)
            {
                if (runtimeEffects[i].source.StartsWith(prefix))
                {
                    runtimeEffects.RemoveAt(i);
                    removed++;
                }
            }
            return removed;
        }

        // ===== 트리거 발동 =====

        /// <summary>
        /// 트리거 발동. 매칭되는 모든 효과를 실행.
        /// Projectile.OnHit, AreaZone.OnTick 등에서 호출.
        /// </summary>
        public void FireTrigger(TriggerType type, TriggerContext context)
        {
            var registry = GetRegistry();
            if (registry == null) return;

            // 기본 효과 실행
            for (int i = 0; i < baseEffects.Count; i++)
            {
                if (baseEffects[i].trigger == type)
                    registry.Execute(baseEffects[i].action, baseEffects[i].parameters, context);
            }

            // 런타임 효과 실행
            for (int i = 0; i < runtimeEffects.Count; i++)
            {
                if (runtimeEffects[i].effect.trigger == type)
                    registry.Execute(runtimeEffects[i].effect.action,
                                     runtimeEffects[i].effect.parameters, context);
            }
        }

        /// <summary>
        /// 특정 트리거 타입에 등록된 효과가 있는지 확인.
        /// 성능 최적화: 효과가 없으면 FireTrigger 호출 자체를 스킵할 수 있음.
        /// </summary>
        public bool HasTrigger(TriggerType type)
        {
            for (int i = 0; i < baseEffects.Count; i++)
            {
                if (baseEffects[i].trigger == type) return true;
            }
            for (int i = 0; i < runtimeEffects.Count; i++)
            {
                if (runtimeEffects[i].effect.trigger == type) return true;
            }
            return false;
        }

        // ===== 레지스트리 =====

        /// <summary>
        /// 공유 레지스트리 접근. 첫 호출 시 자동 생성 + 기본 등록.
        /// TODO: [Post-Release] DI Container로 전환 시 주입 방식으로 변경.
        /// </summary>
        private static EffectActionRegistry GetRegistry()
        {
            if (sharedRegistry == null)
            {
                sharedRegistry = new EffectActionRegistry();
                sharedRegistry.RegisterDefaults();
            }
            return sharedRegistry;
        }

        /// <summary>
        /// 외부에서 공유 레지스트리에 핸들러를 추가 등록할 때 사용.
        /// 예: 정수 시스템에서 커스텀 효과 등록.
        /// </summary>
        public static EffectActionRegistry SharedRegistry => GetRegistry();

        // ===== 디버그 =====

        /// <summary>등록된 전체 효과 수.</summary>
        public int TotalEffectCount => baseEffects.Count + runtimeEffects.Count;

        public string GetDebugString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Base ({baseEffects.Count}):");
            for (int i = 0; i < baseEffects.Count; i++)
                sb.AppendLine($"  {baseEffects[i]}");

            sb.AppendLine($"Runtime ({runtimeEffects.Count}):");
            for (int i = 0; i < runtimeEffects.Count; i++)
                sb.AppendLine($"  {runtimeEffects[i]}");

            return sb.ToString();
        }
    }
}
