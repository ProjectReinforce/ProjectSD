using System.Collections.Generic;
using UnityEngine;
using SwDreams.Domain.ValueObjects;

namespace SwDreams.Adapter.Skill.TriggerEffects
{
    /// <summary>
    /// EffectActionType → IEffectActionHandler 매핑 레지스트리.
    /// SkillEffectFactory와 유사한 패턴.
    ///
    /// 사용법:
    ///   var registry = new EffectActionRegistry();
    ///   registry.RegisterDefaults();
    ///   registry.Execute(EffectActionType.Explode, params, context);
    ///
    /// 확장:
    ///   registry.Register(EffectActionType.NewEffect, new NewEffectHandler());
    ///
    /// [Phase 7 리팩토링] Step 3-2
    /// </summary>
    public class EffectActionRegistry
    {
        private readonly Dictionary<EffectActionType, IEffectActionHandler> handlers
            = new Dictionary<EffectActionType, IEffectActionHandler>();

        // ===== 등록 =====

        public void Register(EffectActionType type, IEffectActionHandler handler)
        {
            if (handler == null)
            {
                Debug.LogError($"[EffectActionRegistry] null handler 등록 시도: {type}");
                return;
            }
            handlers[type] = handler;
        }

        public bool Unregister(EffectActionType type)
        {
            return handlers.Remove(type);
        }

        public bool IsRegistered(EffectActionType type)
        {
            return handlers.ContainsKey(type);
        }

        // ===== 실행 =====

        /// <summary>
        /// 해당 ActionType의 핸들러를 실행.
        /// 미등록이면 경고 로그만 출력 (게임 중단 방지).
        /// </summary>
        public void Execute(EffectActionType type, EffectParams parameters, TriggerContext context)
        {
            if (!handlers.TryGetValue(type, out var handler))
            {
                Debug.LogWarning($"[EffectActionRegistry] 미등록 ActionType: {type}");
                return;
            }

            try
            {
                handler.Execute(parameters, context);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[EffectActionRegistry] {type} 실행 오류: {ex.Message}");
            }
        }

        // ===== 기본 등록 =====

        /// <summary>
        /// 기본 핸들러 등록. 게임 초기화 시 호출.
        /// 새 핸들러 추가 시 여기에 Register 한 줄 추가.
        /// </summary>
        public void RegisterDefaults()
        {
            Register(EffectActionType.Explode, new ExplodeHandler());
            Register(EffectActionType.DealDamage, new DealDamageHandler());
            Register(EffectActionType.ApplySlow, new ApplySlowHandler());
            Register(EffectActionType.Chain, new ChainHandler());
            Register(EffectActionType.Execute, new ExecuteHandler());
            // TODO: 추후 구현 시 등록
            // Register(EffectActionType.ApplyDoT, new ApplyDoTHandler());
            // Register(EffectActionType.Pull, new PullHandler());
            // Register(EffectActionType.Refire, new RefireHandler());
            // Register(EffectActionType.SpawnProjectile, new SpawnProjectileHandler());
            // Register(EffectActionType.ApplyVulnerability, new ApplyVulnerabilityHandler());
            // Register(EffectActionType.HealSelf, new HealSelfHandler());

            Debug.Log($"[EffectActionRegistry] 기본 등록 완료: {handlers.Count}개");
        }
    }
}
