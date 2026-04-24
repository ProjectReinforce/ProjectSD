namespace SwDreams.Shared.Domain.ValueObjects
{
    /// <summary>
    /// 런타임 효과/수정자 소스 네이밍 컨벤션 상수 집합.
    ///
    /// 스킬 트리거 효과(DoT/Slow) 나 EnemyMovement 의 slowStack 에서
    /// `context.source` 가 null/빈 문자열일 때 "기본 단일 슬롯" 으로 통합 처리하기 위한 fallback 키.
    /// 여러 파일에 "__legacy__" 문자열이 중복되던 것을 SSOT 로 집약.
    /// </summary>
    public static class RuntimeSources
    {
        /// <summary>
        /// source 가 지정되지 않은 레거시 단일 슬롯 식별자.
        /// ApplyDoTHandler / ApplySlowHandler / EnemyMovement.slowStack 에서
        /// source 가 null/빈 문자열일 때 이 값으로 치환해 하나의 단일 인스턴스로 관리.
        /// </summary>
        public const string Legacy = "__legacy__";
    }
}
