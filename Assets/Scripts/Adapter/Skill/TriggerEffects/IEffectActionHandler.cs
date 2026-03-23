using SwDreams.Domain.ValueObjects;

namespace SwDreams.Adapter.Skill.TriggerEffects
{
    /// <summary>
    /// 개별 EffectActionType의 실행 로직 인터페이스.
    /// EffectActionRegistry에 등록하여 사용.
    ///
    /// 구현 예: ExplodeHandler, ApplyDoTHandler, ChainHandler 등.
    /// 새 효과 추가 시: 이 인터페이스 구현 + Registry에 등록.
    ///
    /// [Phase 7 리팩토링] Step 3-2
    /// </summary>
    public interface IEffectActionHandler
    {
        /// <summary>
        /// 효과 실행.
        /// </summary>
        /// <param name="parameters">SO 또는 런타임에서 정의된 파라미터.</param>
        /// <param name="context">트리거 발동 시점의 컨텍스트 (위치, 대상 등).</param>
        void Execute(EffectParams parameters, TriggerContext context);
    }
}
